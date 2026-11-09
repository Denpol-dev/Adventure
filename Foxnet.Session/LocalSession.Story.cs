using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed partial class LocalSession
{
    private const string InvoiceId = "n910vt-invoice";
    private const string Conflict = "unknown-conflict";
    private const string Started = "started-at";

    public void EnterWorkspace()
    {
        if (_scenario.Introduction is not { } timing)
            return;

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var now = UtcNow();
        if (Flag(connection, transaction, Started) == null)
        {
            if (Scalar(connection, transaction, "SELECT 1 FROM player_account WHERE singleton = 1;") == null)
                return;

            SetFlag(connection, transaction, Started, now.ToString(CultureInfo.InvariantCulture));
            SetFlag(connection, transaction, Conflict, "open");
            SeedEconomy(connection, transaction, now);
            var intro = _messages["unknown-relay"];
            if (Scalar(connection, transaction, "SELECT 1 FROM messages WHERE id = 'unknown-relay';") != null)
            {
                foreach (var fact in intro.Facts ?? [])
                    AddFact(connection, transaction, intro.ContactId, fact);
                AddHistory(connection, transaction, "first:unknown", "unknown", "Получены данные для доступа к N910VT.", now);
                SetFlag(connection, transaction, "legacy-relay", "yes");
                Deliver(connection, transaction, _messages["unknown-invoice"], now);
            }
            else
                Deliver(connection, transaction, intro, now);
            CreateInvoice(connection, transaction, now);
            Schedule(connection, transaction, "unknown-reminder", "reminder", "unknown-reminder", After(now, timing.ReminderMinutes));
            Schedule(connection, transaction, "journalist-intro", "message", "journalist-intro", After(now, timing.JournalistMinutes));
            Schedule(connection, transaction, "ivan-intro", "message", "ivan-intro", After(now, timing.IvanMinutes), "journalist-intro");
        }
        ProcessDue(connection, transaction, now);
        transaction.Commit();
    }

    public void AdvanceWorld()
    {
        if (_scenario.Introduction == null)
            return;
        using var connection = _database.Open();
        var now = UtcNow();
        if (Scalar(connection, null, """
            SELECT 1 WHERE EXISTS(SELECT 1 FROM npc_wallets WHERE credits_per_minute > 0 AND last_accrual <= $now - 60000)
                OR EXISTS(SELECT 1 FROM story_events e LEFT JOIN story_events p ON p.id = e.prerequisite
                    WHERE e.done = 0 AND e.due_at <= $now AND (e.prerequisite IS NULL OR p.done = 1));
            """, ("now", now)) == null)
            return;
        using var transaction = connection.BeginTransaction();
        ProcessDue(connection, transaction, now);
        transaction.Commit();
    }

    public bool RecordCorporateTrace()
    {
        if (_scenario.Introduction == null)
            return false;
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        if (Flag(connection, transaction, Started) == null)
            return false;

        var now = UtcNow();
        SetFlag(connection, transaction, "corporate-trace", "yes");
        Schedule(connection, transaction, "security-trace", "message", "security-intro",
            After(now, _scenario.Introduction.SecurityMinutes));
        transaction.Commit();
        return true;
    }

    internal void RecordCorporatePressure(long evidence, long risk, long demand, long irritation, long trust,
        bool accessEarned)
    {
        if (_scenario.Introduction == null)
            return;
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        if (Flag(connection, transaction, Started) == null)
            return;
        Execute(connection, transaction, """
            INSERT INTO corporate_pressure(singleton, evidence, risk, demand, irritation, trust)
            VALUES(1, $evidence, $risk, $demand, $irritation, $trust)
            ON CONFLICT(singleton) DO UPDATE SET evidence = excluded.evidence, risk = excluded.risk,
                demand = excluded.demand, irritation = excluded.irritation, trust = excluded.trust;
            """, ("evidence", evidence), ("risk", risk), ("demand", demand), ("irritation", irritation), ("trust", trust));
        if (accessEarned)
            Schedule(connection, transaction, "director-intro", "message", "director-intro", UtcNow());
        transaction.Commit();
    }

    private void ProcessDue(SqliteConnection connection, SqliteTransaction transaction, long now)
    {
        if (Flag(connection, transaction, Started) == null)
            return;
        while (ReadDue(connection, transaction, now) is { } next)
        {
            Accrue(connection, transaction, next.Due);
            switch (next.Kind)
            {
                case "message":
                    Deliver(connection, transaction, _messages[next.Subject], next.Due);
                    break;
                case MessagePartEvent:
                    var part = JsonSerializer.Deserialize<MessagePart>(next.Subject)
                        ?? throw new InvalidDataException("Пустое сообщение FOXNET.");
                    DeliverMessagePart(connection, transaction, next.Id, part, next.Due);
                    break;
                case MessageReplyEvent:
                    var reply = JsonSerializer.Deserialize<MessageReply>(next.Subject)
                        ?? throw new InvalidDataException("Пустой ответ FOXNET.");
                    Deliver(connection, transaction, _messages[reply.MessageId], next.Due);
                    break;
                case "reminder":
                    if (Flag(connection, transaction, Conflict) == "open")
                        Deliver(connection, transaction, _messages[next.Subject], next.Due);
                    break;
                case "deferral":
                    if (Flag(connection, transaction, Conflict) == "deferred")
                    {
                        Deliver(connection, transaction, _messages[next.Subject], next.Due);
                        EndUnknownConflict(connection, transaction, RequestStatus.Expired, next.Due, next.Id);
                    }
                    break;
                case "decide-request":
                    DecideRequest(connection, transaction, next.Subject, next.Due);
                    break;
                case "expire-request":
                    ChangeRequestStatus(connection, transaction, next.Subject, RequestStatus.Expired);
                    break;
                case "contract-payment":
                    SettleContractPayment(connection, transaction, next.Subject, next.Due);
                    break;
                default:
                    throw new InvalidDataException($"Неизвестный тип события: {next.Kind}");
            }
            Execute(connection, transaction, "UPDATE story_events SET done = 1 WHERE id = $id;", ("id", next.Id));
        }
        Accrue(connection, transaction, now);
    }

    private static (string Id, string Kind, string Subject, long Due)? ReadDue(SqliteConnection connection,
        SqliteTransaction transaction, long now)
    {
        using var query = Command(connection, transaction, """
            SELECT e.id, e.kind, e.subject, e.due_at FROM story_events e
            LEFT JOIN story_events p ON p.id = e.prerequisite
            WHERE e.done = 0 AND e.due_at <= $now AND (e.prerequisite IS NULL OR p.done = 1)
            ORDER BY e.due_at, e.rowid LIMIT 1;
            """, ("now", now));
        using var reader = query.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3)) : null;
    }

    private static void Schedule(SqliteConnection connection, SqliteTransaction transaction, string id, string kind,
        string subject, long due, string? prerequisite = null) =>
        Execute(connection, transaction, """
            INSERT INTO story_events(id, kind, subject, due_at, prerequisite) VALUES($id, $kind, $subject, $due, $prerequisite)
            ON CONFLICT(id) DO NOTHING;
            """, ("id", id), ("kind", kind), ("subject", subject), ("due", due), ("prerequisite", prerequisite));

    private static long After(long timestamp, int minutes) => checked(timestamp + minutes * 60_000L);

    private static string? Flag(SqliteConnection connection, SqliteTransaction transaction, string key) =>
        Scalar(connection, transaction, "SELECT value FROM story_flags WHERE key = $key;", ("key", key)) as string;

    private static void SetFlag(SqliteConnection connection, SqliteTransaction transaction, string key, string value) =>
        Execute(connection, transaction, """
            INSERT INTO story_flags(key, value) VALUES($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """, ("key", key), ("value", value));

    private static bool IsReplyAllowed(SqliteConnection connection, SqliteTransaction transaction,
        IncomingDefinition message, ReplyDefinition option)
    {
        if (IsBlocked(connection, transaction, message.ContactId))
            return false;
        if (option.Action == null)
            return true;

        var expected = message.Id switch
        {
            "unknown-relay" when Flag(connection, transaction, "legacy-relay") != null => null,
            "unknown-relay" or "unknown-invoice" => "open",
            "unknown-dispute" => "disputed",
            "unknown-deferred" => "deferred",
            _ => null
        };
        return expected != null && Flag(connection, transaction, Conflict) == expected;
    }

    private void ApplyReply(SqliteConnection connection, SqliteTransaction transaction, ReplyDefinition option, long now)
    {
        switch (option.Action)
        {
            case ReplyAction.Dispute:
                SetFlag(connection, transaction, Conflict, "disputed");
                break;
            case ReplyAction.Refuse:
                EndUnknownConflict(connection, transaction, RequestStatus.Cancelled, now);
                break;
            case ReplyAction.Defer:
                SetFlag(connection, transaction, Conflict, "deferred");
                Schedule(connection, transaction, "unknown-expiry", "deferral", "unknown-expired",
                    After(now, _scenario.Introduction!.DeferralMinutes));
                break;
        }
    }

    private void EndUnknownConflict(SqliteConnection connection, SqliteTransaction transaction,
        RequestStatus status, long now, string? prerequisite = null)
    {
        SetFlag(connection, transaction, Conflict, "disclosed");
        Execute(connection, transaction, "UPDATE contact_state SET blocked = 1 WHERE contact_id = 'unknown';");
        ChangeRequestStatus(connection, transaction, InvoiceId, status);
        Schedule(connection, transaction, "security-disclosure", "message", "security-intro",
            After(now, _scenario.Introduction!.SecurityMinutes), prerequisite);
    }
}
