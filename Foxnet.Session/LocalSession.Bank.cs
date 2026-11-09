using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed partial class LocalSession
{
    private const string OpeningBalanceId = "opening-balance";
    private const string OpeningBalancePurpose = "Открытие счета";

    public BankSnapshot GetBank()
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var balance = PlayerBalance(connection, transaction);
        using var requestQuery = Command(connection, transaction, """
            SELECT p.id, p.contact_id, c.nickname, p.amount, p.purpose, p.incoming, p.status, p.created_at
            FROM payment_requests p JOIN contacts c ON c.id = p.contact_id
            ORDER BY p.created_at DESC, p.rowid DESC;
            """);
        var requests = new List<PaymentRequest>();
        using (var reader = requestQuery.ExecuteReader())
            while (reader.Read())
                requests.Add(new PaymentRequest(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetInt64(3), reader.GetString(4), reader.GetBoolean(5), (RequestStatus)reader.GetInt32(6), At(reader.GetInt64(7))));

        using var historyQuery = Command(connection, transaction, """
            SELECT b.id, COALESCE(c.nickname, 'HAESEI Bank'), b.amount,
                CASE WHEN b.id = $openingId THEN $openingPurpose ELSE b.purpose END, b.happened_at
            FROM bank_entries b LEFT JOIN contacts c ON c.id = b.contact_id
            ORDER BY b.happened_at DESC, b.rowid DESC;
            """, ("openingId", OpeningBalanceId), ("openingPurpose", OpeningBalancePurpose));
        var history = new List<BankEntry>();
        using (var reader = historyQuery.ExecuteReader())
            while (reader.Read())
                history.Add(new BankEntry(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3), At(reader.GetInt64(4))));
        transaction.Commit();
        return new BankSnapshot(balance, requests, history);
    }

    public BankResult Transfer(Guid operationId, string contactId, long amount, string purpose)
    {
        if (!ValidPayment(operationId, amount, purpose))
            return new BankResult(BankError.Invalid);

        return BankWrite((connection, transaction, now) =>
        {
            var id = "transfer:" + operationId;
            using var prior = Command(connection, transaction,
                "SELECT contact_id, amount, purpose FROM bank_entries WHERE id = $id;", ("id", id));
            using var reader = prior.ExecuteReader();
            if (reader.Read())
                return new BankResult(reader.GetString(0) == contactId && reader.GetInt64(1) == -amount &&
                    reader.GetString(2) == purpose ? BankError.None : BankError.IdConflict);
            reader.Close();

            if (!HasBankAddress(connection, transaction, contactId))
                return new BankResult(BankError.Unavailable);
            return PayNpc(connection, transaction, id, contactId, amount, purpose, now);
        });
    }

    public BankResult PayRequest(string requestId) => BankWrite((connection, transaction, now) =>
    {
        if (ReadRequest(connection, transaction, requestId) is not { Incoming: true } request)
            return new BankResult(BankError.Unavailable);
        if (request.Status == RequestStatus.Paid)
            return new BankResult(BankError.None);
        if (request.Status != RequestStatus.Pending)
            return new BankResult(BankError.Unavailable);

        var result = PayNpc(connection, transaction, "invoice:" + requestId, request.Contact, request.Amount, request.Purpose, now);
        if (result.Success)
        {
            ChangeRequestStatus(connection, transaction, requestId, RequestStatus.Paid, notify: false);
            if (requestId == InvoiceId)
            {
                SetFlag(connection, transaction, Conflict, "paid");
                Deliver(connection, transaction, _messages["unknown-paid"], now);
            }
        }
        return result;
    });

    public BankResult RequestPayment(Guid operationId, string contactId, long amount, string purpose)
    {
        if (!ValidPayment(operationId, amount, purpose) || _scenario.Introduction is not { } timing)
            return new BankResult(BankError.Invalid);

        return BankWrite((connection, transaction, now) =>
        {
            var id = "request:" + operationId;
            if (ReadRequest(connection, transaction, id) is { } prior)
                return new BankResult(prior.Contact == contactId && prior.Amount == amount && prior.Purpose == purpose
                    ? BankError.None : BankError.IdConflict);
            if (!HasBankAddress(connection, transaction, contactId) || IsBlocked(connection, transaction, contactId))
                return new BankResult(BankError.Unavailable);
            if (Scalar(connection, transaction, """
                SELECT 1 FROM payment_requests WHERE contact_id = $id AND incoming = 0 AND status = 0 LIMIT 1;
                """, ("id", contactId)) != null)
                return new BankResult(BankError.AlreadyPending);
            if (Scalar(connection, transaction, """
                SELECT MAX(created_at) FROM payment_requests WHERE contact_id = $id AND incoming = 0;
                """, ("id", contactId)) is long last && now < After(last, timing.RequestCooldownMinutes))
                return new BankResult(BankError.RateLimited);

            var expires = After(now, timing.RequestExpiryMinutes);
            AddRequest(connection, transaction, id, contactId, amount, purpose, false, now, expires);
            Schedule(connection, transaction, id + ":decision", "decide-request", id, After(now, timing.RequestDecisionMinutes));
            Schedule(connection, transaction, id + ":expiry", "expire-request", id, expires, id + ":decision");
            return new BankResult(BankError.None);
        });
    }

    public BankResult CancelRequest(string requestId) => BankWrite((connection, transaction, _) =>
    {
        if (ReadRequest(connection, transaction, requestId) is not { Incoming: false } request ||
            request.Status is not (RequestStatus.Pending or RequestStatus.Cancelled))
            return new BankResult(BankError.Unavailable);
        ChangeRequestStatus(connection, transaction, requestId, RequestStatus.Cancelled, notify: false);
        return new BankResult(BankError.None);
    });

    public BankResult ScheduleContractPayment(Guid operationId, string contactId, long amount, string purpose,
        DateTimeOffset due)
    {
        if (!ValidPayment(operationId, amount, purpose))
            return new BankResult(BankError.Invalid);

        return BankWrite((connection, transaction, now) =>
        {
            var id = "contract:" + operationId;
            var dueAt = due.ToUnixTimeMilliseconds();
            using var prior = Command(connection, transaction, """
                SELECT contact_id, amount, purpose, due_at FROM contract_payments WHERE id = $id;
                """, ("id", id));
            using var reader = prior.ExecuteReader();
            if (reader.Read())
                return new BankResult(reader.GetString(0) == contactId && reader.GetInt64(1) == amount &&
                    reader.GetString(2) == purpose && reader.GetInt64(3) == dueAt ? BankError.None : BankError.IdConflict);
            reader.Close();

            if (!HasBankAddress(connection, transaction, contactId))
                return new BankResult(BankError.Unavailable);
            Execute(connection, transaction, """
                INSERT INTO contract_payments(id, contact_id, amount, purpose, due_at)
                VALUES($id, $contact, $amount, $purpose, $due);
                """, ("id", id), ("contact", contactId), ("amount", amount), ("purpose", purpose), ("due", dueAt));
            Schedule(connection, transaction, id, "contract-payment", id, Math.Max(dueAt, now));
            ProcessDue(connection, transaction, now);
            return new BankResult(BankError.None);
        });
    }

    private static void SettleContractPayment(SqliteConnection connection, SqliteTransaction transaction, string id, long now)
    {
        using var query = Command(connection, transaction, """
            SELECT contact_id, amount, purpose FROM contract_payments WHERE id = $id AND status = 0;
            """, ("id", id));
        using var reader = query.ExecuteReader();
        if (!reader.Read())
            return;
        var contact = reader.GetString(0);
        var amount = reader.GetInt64(1);
        var purpose = reader.GetString(2);
        reader.Close();

        var paid = NpcBalance(connection, transaction, contact) >= amount;
        if (paid)
            ReceiveCredits(connection, transaction, id, contact, amount, purpose, now);
        Execute(connection, transaction, "UPDATE contract_payments SET status = $status WHERE id = $id;",
            ("status", (int)(paid ? RequestStatus.Paid : RequestStatus.Rejected)), ("id", id));
    }

    private BankResult BankWrite(Func<SqliteConnection, SqliteTransaction, long, BankResult> operation)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var now = UtcNow();
        ProcessDue(connection, transaction, now);
        var result = operation(connection, transaction, now);
        transaction.Commit();
        return result;
    }

    private void SeedEconomy(SqliteConnection connection, SqliteTransaction transaction, long now)
    {
        Execute(connection, transaction, "INSERT INTO player_wallet(singleton, balance) VALUES(1, 1673);");
        AddEntry(connection, transaction, OpeningBalanceId, null, 3173, OpeningBalancePurpose, checked(now - 2));
        AddEntry(connection, transaction, "n910vt-advance", "unknown", -1500, "Аванс за подключение N910VT", checked(now - 1));
        foreach (var contact in _scenario.Contacts)
            if (contact.Details is { } npc)
                Execute(connection, transaction, """
                    INSERT INTO npc_wallets(contact_id, balance, credits_per_minute, last_accrual) VALUES($id, $balance, $rate, $now);
                    """, ("id", contact.Id), ("balance", npc.InitialBalance), ("rate", npc.CreditsPerMinute), ("now", now));
    }

    private static void Accrue(SqliteConnection connection, SqliteTransaction transaction, long now)
    {
        using var query = Command(connection, transaction, """
            SELECT contact_id, balance, credits_per_minute, last_accrual FROM npc_wallets
            WHERE credits_per_minute > 0 AND last_accrual <= $now - 60000;
            """, ("now", now));
        var updates = new List<(string Id, long Balance, long Last, long Income)>();
        using (var reader = query.ExecuteReader())
            while (reader.Read())
            {
                var last = reader.GetInt64(3);
                var minutes = checked((now - last) / 60_000L);
                var income = checked(minutes * reader.GetInt64(2));
                updates.Add((reader.GetString(0), checked(reader.GetInt64(1) + income),
                    checked(last + minutes * 60_000L), income));
            }
        foreach (var update in updates)
        {
            Execute(connection, transaction, "UPDATE npc_wallets SET balance = $balance, last_accrual = $last WHERE contact_id = $id;",
                ("balance", update.Balance), ("last", update.Last), ("id", update.Id));
            AddNpcEntry(connection, transaction, $"income:{update.Id}:{update.Last}", update.Id, update.Income, "income", update.Last);
        }
    }

    private static void CreateInvoice(SqliteConnection connection, SqliteTransaction transaction, long now)
    {
        AddRequest(connection, transaction, InvoiceId, "unknown", 10_000, "Оплата за подключение N910VT", true, now, null);
        Notify(connection, transaction, "request:" + InvoiceId, bank: true);
        AddHistory(connection, transaction, "bank:" + InvoiceId, "unknown", "Запрошена оплата за подключение N910VT.", now);
    }

    private static void AddRequest(SqliteConnection connection, SqliteTransaction transaction, string id, string contactId,
        long amount, string purpose, bool incoming, long now, long? expires) =>
        Execute(connection, transaction, """
            INSERT INTO payment_requests(id, contact_id, amount, purpose, incoming, status, created_at, expires_at)
            VALUES($id, $contact, $amount, $purpose, $incoming, 0, $now, $expires);
            """, ("id", id), ("contact", contactId), ("amount", amount), ("purpose", purpose), ("incoming", incoming),
            ("now", now), ("expires", expires));

    private static (string Contact, long Amount, string Purpose, bool Incoming, RequestStatus Status)? ReadRequest(
        SqliteConnection connection, SqliteTransaction transaction, string id)
    {
        using var query = Command(connection, transaction, """
            SELECT contact_id, amount, purpose, incoming, status FROM payment_requests WHERE id = $id;
            """, ("id", id));
        using var reader = query.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetBoolean(3),
            (RequestStatus)reader.GetInt32(4)) : null;
    }

    private static void ChangeRequestStatus(SqliteConnection connection, SqliteTransaction transaction, string id,
        RequestStatus status, bool notify = true)
    {
        if (Execute(connection, transaction, "UPDATE payment_requests SET status = $status WHERE id = $id AND status = 0;",
                ("status", (int)status), ("id", id)) == 1 && notify)
            Notify(connection, transaction, $"request:{id}:{status}", bank: true);
    }

    private void DecideRequest(SqliteConnection connection, SqliteTransaction transaction, string id, long now)
    {
        if (ReadRequest(connection, transaction, id) is not { Status: RequestStatus.Pending } request)
            return;
        var npc = _contacts.GetValueOrDefault(request.Contact)?.Details;
        var relation = Flag(connection, transaction, "relationship:" + request.Contact);
        bool inConflict = request.Contact == "unknown" && Flag(connection, transaction, Conflict) != "paid";
        if (npc == null || request.Amount > npc.RequestLimit || relation == "later" || inConflict ||
            IsBlocked(connection, transaction, request.Contact) || (long)Scalar(connection, transaction, """
                SELECT COUNT(*) FROM payment_requests WHERE contact_id = $id AND incoming = 0 AND status = $paid;
                """, ("id", request.Contact), ("paid", (int)RequestStatus.Paid))! >= npc.MaxPaidRequests)
            ChangeRequestStatus(connection, transaction, id, RequestStatus.Rejected);
        else if (NpcBalance(connection, transaction, request.Contact) >= request.Amount)
        {
            ReceiveCredits(connection, transaction, "received:" + id, request.Contact, request.Amount, request.Purpose, now);
            ChangeRequestStatus(connection, transaction, id, RequestStatus.Paid, notify: false);
        }
    }

    private static bool ValidPayment(Guid id, long amount, string purpose) =>
        id != Guid.Empty && amount > 0 && !string.IsNullOrWhiteSpace(purpose) && purpose.Length <= 240;

    private static bool HasBankAddress(SqliteConnection connection, SqliteTransaction transaction, string contactId) =>
        Scalar(connection, transaction, """
            SELECT 1 FROM npc_wallets w JOIN contact_state s ON s.contact_id = w.contact_id
            WHERE w.contact_id = $id AND s.known = 1;
            """, ("id", contactId)) != null;

    private static long PlayerBalance(SqliteConnection connection, SqliteTransaction transaction) =>
        (long?)Scalar(connection, transaction, "SELECT balance FROM player_wallet WHERE singleton = 1;") ?? 0;

    private static long NpcBalance(SqliteConnection connection, SqliteTransaction transaction, string contactId) =>
        (long)Scalar(connection, transaction, "SELECT balance FROM npc_wallets WHERE contact_id = $id;", ("id", contactId))!;

    private static void SetPlayerBalance(SqliteConnection connection, SqliteTransaction transaction, long balance) =>
        Execute(connection, transaction, "UPDATE player_wallet SET balance = $balance WHERE singleton = 1;", ("balance", balance));

    private static void SetNpcBalance(SqliteConnection connection, SqliteTransaction transaction, string contactId, long balance) =>
        Execute(connection, transaction, "UPDATE npc_wallets SET balance = $balance WHERE contact_id = $id;",
            ("id", contactId), ("balance", balance));

    private static BankResult PayNpc(SqliteConnection connection, SqliteTransaction transaction, string id, string contactId,
        long amount, string purpose, long now)
    {
        var balance = PlayerBalance(connection, transaction);
        if (amount > balance)
            return new BankResult(BankError.InsufficientFunds, balance, amount - balance);

        SetNpcBalance(connection, transaction, contactId, checked(NpcBalance(connection, transaction, contactId) + amount));
        AddNpcEntry(connection, transaction, "player:" + id, contactId, amount, "player-transfer", now);
        SetPlayerBalance(connection, transaction, balance - amount);
        AddEntry(connection, transaction, id, contactId, -amount, purpose, now);
        AddHistory(connection, transaction, "bank:" + id, contactId, $"Перевод игрока. {purpose}", now);
        return new BankResult(BankError.None, balance - amount);
    }

    private static void ReceiveCredits(SqliteConnection connection, SqliteTransaction transaction, string id, string contactId,
        long amount, string purpose, long now)
    {
        SetNpcBalance(connection, transaction, contactId, checked(NpcBalance(connection, transaction, contactId) - amount));
        AddNpcEntry(connection, transaction, "player:" + id, contactId, -amount, "player-payment", now);
        SetPlayerBalance(connection, transaction, checked(PlayerBalance(connection, transaction) + amount));
        AddEntry(connection, transaction, id, contactId, amount, purpose, now);
        Notify(connection, transaction, "bank:" + id, bank: true);
        AddHistory(connection, transaction, "bank:" + id, contactId, $"Получен перевод. {purpose}", now);
    }

    private static void AddNpcEntry(SqliteConnection connection, SqliteTransaction transaction, string id, string contactId,
        long amount, string kind, long now) => Execute(connection, transaction, """
            INSERT INTO npc_ledger(id, contact_id, amount, kind, happened_at) VALUES($id, $contact, $amount, $kind, $now);
            """, ("id", id), ("contact", contactId), ("amount", amount), ("kind", kind), ("now", now));

    private static void AddEntry(SqliteConnection connection, SqliteTransaction transaction, string id, string? contactId,
        long amount, string purpose, long now) =>
        Execute(connection, transaction, """
            INSERT INTO bank_entries(id, contact_id, amount, purpose, happened_at) VALUES($id, $contact, $amount, $purpose, $now);
            """, ("id", id), ("contact", contactId), ("amount", amount), ("purpose", purpose), ("now", now));
}
