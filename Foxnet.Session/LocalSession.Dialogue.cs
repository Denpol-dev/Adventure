using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public enum ChatResult { Unavailable, Invalid, WaitingForReply, Scripted, Generated, ProviderUnavailable }

public sealed partial class LocalSession
{
    private const string ChatFallbackEvent = "chat-fallback";
    public const int ChatLengthLimit = 1200;

    public bool HasDialogueProvider => _dialogue != null;
    private sealed record ChatFallback(string ContactId, string Text, bool Final, bool Refresh = false);

    public async Task<ChatResult> SendChatAsync(string contactId, string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        text = text.Trim();
        if (text.Length is < 1 or > ChatLengthLimit || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            return ChatResult.Invalid;
        var (result, id, context) = StartDialogue(contactId, text);
        if (context == null)
            return result;

        DialogueDecision? decision;
        try
        {
            decision = await _dialogue!.ReplyAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            decision = null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (decision != null && CompleteDialogue(id!, contactId, context, decision) is { } completed)
            return completed;

        ExpediteDialogueFallback(id!);
        return ChatResult.ProviderUnavailable;
    }

    private (ChatResult Result, string? Id, DialogueContext? Context) StartDialogue(string contactId, string text)
    {
        if (_contacts.GetValueOrDefault(contactId) is not { Details.Dialogue: { } persona } contact)
            return (ChatResult.Unavailable, null, null);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var now = UtcNow();
        ProcessDue(connection, transaction, now);
        bool known = Scalar(connection, transaction, """
            SELECT 1 FROM contact_state WHERE contact_id = $id AND known = 1 AND blocked = 0;
            """, ("id", contactId)) != null;
        if (!known)
        {
            transaction.Commit();
            return (ChatResult.Unavailable, null, null);
        }
        if (NextIncomingMessageAt(connection, transaction, contactId) != null)
        {
            transaction.Commit();
            return (ChatResult.WaitingForReply, null, null);
        }

        string id = Guid.NewGuid().ToString("N");
        var (trust, discipline) = TrackDialogue(connection, transaction, contactId, persona, text, id, now);
        if (_dialogue == null && discipline == null && contactId == "unknown" &&
            TrySelectPaymentReply(connection, transaction, text, now))
        {
            transaction.Commit();
            return (ChatResult.Scripted, null, null);
        }

        WriteOutgoingMessage(connection, transaction, "chat:" + id, contactId, text, now);
        string replyId = "chat-reply:" + id;
        string relationship = Flag(connection, transaction, "relationship:" + contactId) ?? "знакомство";
        string fallback = discipline ?? ScenarioReply(connection, transaction, contactId, persona, relationship);
        DialogueContext? context = discipline == null && _dialogue != null && _scenario.Introduction != null
            ? ReadDialogueContext(connection, transaction, contact, persona, trust, relationship) : null;
        if (_scenario.Introduction == null)
            DeliverMessagePart(connection, transaction, replyId, new(contactId, fallback), now);
        else
            Schedule(connection, transaction, replyId, ChatFallbackEvent,
                JsonSerializer.Serialize(new ChatFallback(contactId, fallback, IsBlocked(connection, transaction, contactId),
                    Refresh: context != null)),
                checked(now + (context == null ? ReplyReadingMilliseconds + MessagePauseMilliseconds : 30_000)));
        transaction.Commit();
        return (ChatResult.Scripted, replyId, context);
    }

    private bool TrySelectPaymentReply(SqliteConnection connection, SqliteTransaction transaction, string text, long now)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        const string amount = @"(?:3[ \t]*(?:[кk]|000|тыс(?:яч[аиу])?)|три\s+тысячи)\b";
        const string agreed = @"договор(?:ились|ивались)";
        bool dispute = text.IndexOfAny(['"', '«', '»', '“', '”']) < 0 &&
            Regex.IsMatch(text, $@"(?:^|[.!?]\s+)(?:мы\s+(?:же\s+)?)?(?:{agreed}\s+на\s+{amount}|на\s+{amount}\.?\s+(?:же\s+)?{agreed}\b)", options) &&
            !Regex.IsMatch(text, @"\b(?:но|однако|хотя|если|или)\b", options);
        bool defer = Regex.IsMatch(text, @"\A(?:дай(?:те)?(?:\s+мне)?\s+отсрочку|можно(?:\s+мне)?\s+отсрочку)(?:,\s*пожалуйста)?[.!?]?\z", options);
        ReplyAction? action = dispute ? ReplyAction.Dispute : defer ? ReplyAction.Defer : null;
        if (action == null)
            return false;
        foreach (string id in new[] { "unknown-relay", "unknown-invoice", "unknown-dispute" })
        {
            if (_messages.TryGetValue(id, out var message) &&
                message.Replies.Find(option => option.Action == action && !option.Silent) is { } option &&
                TrySelectReply(connection, transaction, message, option, now, text))
                return true;
        }
        return false;
    }

    private ChatResult? CompleteDialogue(string id, string contactId, DialogueContext context, DialogueDecision decision)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        long now = UtcNow();
        ProcessDue(connection, transaction, now);
        ChatResult? result = null;
        if (!IsBlocked(connection, transaction, contactId) && Scalar(connection, transaction, """
                SELECT 1 FROM story_events WHERE id = $id AND done = 0;
                """, ("id", id)) != null)
        {
            if (decision.Choice > 0 && decision.Choice <= context.Choices.Count &&
                NextIncomingMessageAt(connection, transaction, contactId, except: id) == null)
            {
                var selection = context.Choices[decision.Choice - 1];
                var message = _messages[selection.MessageId];
                var option = message.Replies.Single(reply => reply.Id == selection.Id);
                if (TrySelectReply(connection, transaction, message, option, now, outgoingText: null))
                    result = ChatResult.Scripted;
            }
            else if (decision.Choice == 0 && decision.Reply?.Trim() is { Length: > 0 and <= ChatLengthLimit } reply &&
                !reply.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')) &&
                !string.Equals(reply, context.Turns.LastOrDefault(turn => !turn.Outgoing)?.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                DeliverMessagePart(connection, transaction, id, new(contactId, reply), now);
                result = ChatResult.Generated;
            }
        }
        if (result != null)
            Execute(connection, transaction, "UPDATE story_events SET done = 1 WHERE id = $id;", ("id", id));
        transaction.Commit();
        return result;
    }

    private void ExpediteDialogueFallback(string id)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        long now = UtcNow();
        Execute(connection, transaction, """
            UPDATE story_events SET due_at = $now WHERE id = $id AND done = 0;
            """, ("id", id), ("now", now));
        ProcessDue(connection, transaction, now);
        transaction.Commit();
    }

    private void DeliverDialogueFallback(SqliteConnection connection, SqliteTransaction transaction, string id,
        ChatFallback fallback, long now)
    {
        if (!fallback.Final && IsBlocked(connection, transaction, fallback.ContactId))
            return;
        string text = fallback.Text;
        if (fallback.Refresh && _contacts.GetValueOrDefault(fallback.ContactId)?.Details?.Dialogue is { } persona)
        {
            var relationship = Flag(connection, transaction, "relationship:" + fallback.ContactId) ?? "знакомство";
            text = ScenarioReply(connection, transaction, fallback.ContactId, persona, relationship);
        }
        DeliverMessagePart(connection, transaction, id, new(fallback.ContactId, text), now);
    }

    private static (int Trust, string? Discipline) TrackDialogue(SqliteConnection connection, SqliteTransaction transaction,
        string contactId, DialoguePersona persona, string text, string id, long now)
    {
        int trust = 0;
        int warnings = 0;
        using (var query = Command(connection, transaction, """
            SELECT trust, warnings FROM npc_dialogue WHERE contact_id = $id;
            """, ("id", contactId)))
        using (var reader = query.ExecuteReader())
        {
            if (reader.Read())
                (trust, warnings) = (reader.GetInt32(0), reader.GetInt32(1));
        }

        // Boundary reactions require an explicit first-person threat at the start of a sentence.
        var normalized = text.ToLowerInvariant();
        bool hostile = Regex.IsMatch(normalized, @"(?:^|[.!?]\s+)(?:я (?:тебя|вас) (?:убью|найду и уничтожу)|если (?:ты |вы )?не заплатишь,? я (?:тебя |вас )?сдам)\b");
        bool apologizes = normalized.StartsWith("извини", StringComparison.Ordinal) || normalized.StartsWith("прошу прощения", StringComparison.Ordinal);
        if (!hostile && !apologizes)
            return (trust, null);

        trust = Math.Clamp(trust + (hostile ? -2 : 1), -5, 5);
        warnings = hostile ? warnings + 1 : Math.Max(0, warnings - 1);
        Execute(connection, transaction, """
            INSERT INTO npc_dialogue(contact_id, trust, warnings) VALUES($id, $trust, $warnings)
            ON CONFLICT(contact_id) DO UPDATE SET trust = excluded.trust, warnings = excluded.warnings;
            """, ("id", contactId), ("trust", trust), ("warnings", warnings));
        if (!hostile)
            return (trust, null);

        bool block = warnings >= persona.WarningsBeforeBlock && trust <= -4;
        AddHistory(connection, transaction, "boundary:" + id, contactId,
            block ? "Собеседник прекратил контакт после повторных прямых угроз." : "Собеседник предупредил о границах общения.", now);
        if (block)
            Execute(connection, transaction, "UPDATE contact_state SET blocked = 1 WHERE contact_id = $id;", ("id", contactId));
        return (trust, block ? persona.Block : persona.Warning);
    }

    private static string ScenarioReply(SqliteConnection connection, SqliteTransaction transaction, string contactId,
        DialoguePersona persona, string relationship) => contactId switch
    {
        "unknown" when Flag(connection, transaction, Conflict) == "deferred" =>
            "Отсрочку ты получил. Срок остался прежним, перевод жду в HAESEI Bank.",
        "unknown" when Flag(connection, transaction, Conflict) == "paid" => "Расчёт получил. По подключению мы квиты.",
        "unknown" when Flag(connection, transaction, Conflict) == "disputed" =>
            "Я уже объяснил, откуда сумма. Если тебе нужно время, попроси отсрочку.",
        "journalist" when relationship == "later" => "Можем вернуться к разговору. Если есть проверяемый материал, пришлите его из библиотеки FOXNET.",
        "security" when CurrentHypothesis(connection, transaction) == SecurityHypothesis.SeparateCollaborators =>
            "В распоряжении службы два опубликованных журнала. Выводы о конкретных людях пока преждевременны.",
        _ => persona.Reply
    };

    private DialogueContext ReadDialogueContext(SqliteConnection connection, SqliteTransaction transaction,
        ContactDefinition contact, DialoguePersona persona, int trust, string relationship)
    {
        var observations = new List<FactDefinition>();
        using (var query = Command(connection, transaction, """
            SELECT id, text, source, confidence FROM npc_observations
            WHERE contact_id = $id ORDER BY rowid DESC LIMIT 16;
            """, ("id", contact.Id)))
        using (var reader = query.ExecuteReader())
        {
            while (reader.Read())
                observations.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), (FactConfidence)reader.GetInt32(3)));
        }
        if (contact.Id == "unknown" && Flag(connection, transaction, Conflict) is "open" or "disputed" or "deferred")
            observations.Add(new("relay-agreement",
                "Изначально вы договорились на 3 000 кредитов. Игрок уже внёс 1 500 авансом. Ты выставил новый счёт на 10 000. " +
                "Игрок ещё не оплатил новый счёт. Возражение против новой суммы не является её подтверждением.",
                "Твой договор и счёт за подключение", FactConfidence.Confirmed));
        var turns = new List<DialogueTurn>();
        using (var query = Command(connection, transaction, """
            SELECT outgoing, text FROM messages WHERE contact_id = $id ORDER BY rowid DESC LIMIT 16;
            """, ("id", contact.Id)))
        using (var reader = query.ExecuteReader())
        {
            while (reader.Read())
                turns.Add(new(reader.GetBoolean(0), reader.GetString(1)));
        }
        turns.Reverse();
        var choices = new List<ReplyOption>();
        using var messages = Command(connection, transaction, """
            SELECT m.id FROM messages m LEFT JOIN selected_replies s ON s.message_id = m.id
            WHERE m.contact_id = $contact AND m.outgoing = 0 AND s.message_id IS NULL ORDER BY m.rowid;
            """, ("contact", contact.Id));
        using var incoming = messages.ExecuteReader();
        while (incoming.Read())
            choices.AddRange(AvailableReplies(connection, transaction, incoming.GetString(0)).Where(option => !option.Silent));
        return new(contact.Nickname, persona.Character, trust, relationship, observations, turns, choices.AsReadOnly());
    }
}
