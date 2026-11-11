using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed partial class LocalSession
{
    private const string MessagePartEvent = "message-part";
    private const string MessageReplyEvent = "message-reply";
    private const long ReplyReadingMilliseconds = 2000;
    private const long MessagePauseMilliseconds = 3000;

    public bool IsContactTyping(string contactId) => GetContactTypingDelay(contactId) == TimeSpan.Zero;

    public TimeSpan? GetContactTypingDelay(string contactId)
    {
        using var connection = _database.Open();
        if (NextIncomingMessageAt(connection, null, contactId) is not { } dueAt)
            return null;

        return TimeSpan.FromMilliseconds(Math.Max(0, dueAt - UtcNow() - MessagePauseMilliseconds));
    }

    private static long? NextIncomingMessageAt(SqliteConnection connection, SqliteTransaction? transaction, string contactId,
        string? except = null) =>
        Scalar(connection, transaction, """
            SELECT MIN(due_at) FROM story_events
            WHERE done = 0 AND id IS NOT $except AND CASE WHEN kind IN ($part, $reply, $chat)
                THEN json_extract(subject, '$.ContactId') END = $contact;
            """, ("part", MessagePartEvent), ("reply", MessageReplyEvent), ("chat", ChatFallbackEvent),
            ("contact", contactId), ("except", except)) as long?;

    public IReadOnlyList<Conversation> GetConversations()
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, """
            SELECT c.id, c.nickname, latest.text, latest.sent_at,
                (SELECT COUNT(*) FROM messages m WHERE m.contact_id = c.id AND m.is_read = 0)
            FROM contacts c
            JOIN messages latest ON latest.rowid =
                (SELECT m.rowid FROM messages m WHERE m.contact_id = c.id ORDER BY m.rowid DESC LIMIT 1)
            ORDER BY latest.rowid DESC;
            """);
        using var reader = query.ExecuteReader();
        var conversations = new List<Conversation>();
        while (reader.Read())
            conversations.Add(new Conversation(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)), reader.GetInt32(4)));
        return conversations;
    }

    public IReadOnlyList<Message> OpenConversation(string contactId, bool markRead = true)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: !markRead);
        if (markRead)
            Execute(connection, transaction, "UPDATE messages SET is_read = 1 WHERE contact_id = $id AND is_read = 0;",
                ("id", contactId));

        var attachments = ReadAttachments(connection, transaction, contactId);
        var waitingForMessage = NextIncomingMessageAt(connection, transaction, contactId) != null;
        using var query = Command(connection, transaction, """
            SELECT m.id, m.text, m.sent_at, m.outgoing, s.option_id
            FROM messages m LEFT JOIN selected_replies s ON s.message_id = m.id
            WHERE m.contact_id = $id ORDER BY m.rowid;
            """, ("id", contactId));
        using var reader = query.ExecuteReader();
        var messages = new List<Message>();
        while (reader.Read())
        {
            string id = reader.GetString(0);
            var replies = !waitingForMessage && reader.IsDBNull(4)
                ? AvailableReplies(connection, transaction, id).ToArray()
                : [];
            messages.Add(new Message(id, contactId, reader.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)),
                reader.GetBoolean(3), attachments.GetValueOrDefault(id) ?? [], replies));
        }
        reader.Close();
        transaction.Commit();
        return messages;
    }

    private IEnumerable<ReplyOption> AvailableReplies(SqliteConnection connection, SqliteTransaction transaction, string id)
    {
        if (!_messages.TryGetValue(id, out var definition))
            yield break;
        foreach (var option in definition.Replies)
        {
            if (IsReplyAllowed(connection, transaction, definition, option) &&
                (!option.Independent || Scalar(connection, transaction, """
                    SELECT 1 FROM independent_replies WHERE message_id = $message AND option_id = $option;
                    """, ("message", id), ("option", option.Id)) == null))
                yield return new ReplyOption(id, option.Id, option.Text, option.Silent);
        }
    }

    public bool SelectReply(string messageId, string optionId)
    {
        if (!_messages.TryGetValue(messageId, out var message) ||
            message.Replies.Find(reply => reply.Id == optionId) is not ReplyDefinition option)
            return false;

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var now = UtcNow();
        ProcessDue(connection, transaction, now);
        bool selected = NextIncomingMessageAt(connection, transaction, message.ContactId) == null &&
            TrySelectReply(connection, transaction, message, option, now, option.Text);
        transaction.Commit();
        return selected;
    }

    private bool TrySelectReply(SqliteConnection connection, SqliteTransaction transaction, IncomingDefinition message,
        ReplyDefinition option, long now, string? outgoingText)
    {
        var select = option.Independent ? """
            INSERT INTO independent_replies(message_id, option_id)
            SELECT id, $option FROM messages WHERE id = $message AND outgoing = 0
                AND NOT EXISTS (SELECT 1 FROM selected_replies WHERE message_id = $message)
            ON CONFLICT(message_id, option_id) DO NOTHING;
            """ : """
            INSERT INTO selected_replies(message_id, option_id)
            SELECT id, $option FROM messages WHERE id = $message AND outgoing = 0
            ON CONFLICT(message_id) DO NOTHING;
            """;
        if (!IsReplyAllowed(connection, transaction, message, option) ||
            Execute(connection, transaction, select, ("message", message.Id), ("option", option.Id)) == 0)
            return false;

        Execute(connection, transaction, "UPDATE messages SET is_read = 1 WHERE id = $id;", ("id", message.Id));

        if (!option.Silent && outgoingText != null)
            WriteOutgoingMessage(connection, transaction, Guid.NewGuid().ToString("D"), message.ContactId,
                outgoingText, now);

        if (option.NextMessageId is string nextId)
        {
            var next = _messages[nextId];
            if (_scenario.Introduction == null)
                Deliver(connection, transaction, next, now);
            else if (Scalar(connection, transaction, "SELECT 1 FROM messages WHERE id = $id;", ("id", nextId)) == null)
                Schedule(connection, transaction, "reply:" + nextId, MessageReplyEvent,
                    JsonSerializer.Serialize(new MessageReply(next.ContactId, nextId)),
                    checked(now + ReplyReadingMilliseconds + MessagePauseMilliseconds));
        }
        if (option.Relationship is { } relationship)
            SetFlag(connection, transaction, "relationship:" + message.ContactId, relationship);
        ApplyReply(connection, transaction, option, now);
        if (option.UnlockDeviceId is string deviceId)
            Execute(connection, transaction, "UPDATE devices SET available = 1 WHERE id = $id;", ("id", deviceId));

        return true;
    }

    private static void WriteOutgoingMessage(SqliteConnection connection, SqliteTransaction transaction, string id,
        string contactId, string text, long now) =>
        Execute(connection, transaction, """
            INSERT INTO messages(id, contact_id, text, sent_at, outgoing, is_read)
            VALUES($id, $contact, $text, $sentAt, 1, 1);
            """, ("id", id), ("contact", contactId), ("text", text), ("sentAt", now));

    private void Deliver(SqliteConnection connection, SqliteTransaction transaction, IncomingDefinition message, long now,
        string? instanceId = null)
    {
        var id = instanceId ?? message.Id;
        var content = _scenario.Render(message.Text, message);
        var parts = _scenario.Introduction == null ? [content] : content.ReplaceLineEndings("\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!DeliverMessagePart(connection, transaction, id, new MessagePart(message.ContactId, parts[0]), now))
            return;

        for (var index = 1; index < parts.Length; index++)
        {
            var part = new MessagePart(message.ContactId, parts[index]);
            Schedule(connection, transaction, $"{id}:part:{index}", MessagePartEvent, JsonSerializer.Serialize(part),
                checked(now + index * MessagePauseMilliseconds));
        }

        var revealed = Execute(connection, transaction, """
            INSERT INTO contact_state(contact_id, known) VALUES($contact, 1)
            ON CONFLICT(contact_id) DO UPDATE SET known = 1 WHERE known = 0;
            """, ("contact", message.ContactId));
        if (revealed == 1)
            AddHistory(connection, transaction, "first:" + message.ContactId, message.ContactId, "Первое сообщение.", now);
        foreach (var fact in message.Facts ?? [])
            AddFact(connection, transaction, message.ContactId, fact);
        if (message.History is { } history)
            AddHistory(connection, transaction, "story:" + id, message.ContactId, history, now);
        foreach (var attachment in message.Attachments)
        {
            Execute(connection, transaction, """
                INSERT INTO attachments(event_id, message_id, title, file_name, format, content)
                VALUES($eventId, $messageId, $title, $fileName, $format, $content);
                """, ("eventId", attachment.EventId), ("messageId", id), ("title", attachment.Title),
                ("fileName", attachment.FileName), ("format", (int)attachment.Format),
                ("content", Encoding.UTF8.GetBytes(_scenario.Render(attachment.Text, message))));
        }
    }

    private static bool DeliverMessagePart(SqliteConnection connection, SqliteTransaction transaction,
        string id, MessagePart part, long now)
    {
        var added = Execute(connection, transaction, """
            INSERT INTO messages(id, contact_id, text, sent_at, outgoing, is_read)
            VALUES($id, $contact, $text, $sentAt, 0, 0) ON CONFLICT(id) DO NOTHING;
            """, ("id", id), ("contact", part.ContactId), ("text", part.Text), ("sentAt", now)) == 1;
        if (added)
            Notify(connection, transaction, "message:" + id, bank: false);
        return added;
    }

    private sealed record MessagePart(string ContactId, string Text);
    private sealed record MessageReply(string ContactId, string MessageId);

    private static Dictionary<string, List<Attachment>> ReadAttachments(SqliteConnection connection,
        SqliteTransaction transaction, string contactId)
    {
        using var query = Command(connection, transaction, """
            SELECT a.message_id, a.event_id, a.title, a.file_name, a.format
            FROM attachments a JOIN messages m ON m.id = a.message_id WHERE m.contact_id = $id ORDER BY a.rowid;
            """, ("id", contactId));
        using var reader = query.ExecuteReader();
        var attachments = new Dictionary<string, List<Attachment>>();
        while (reader.Read())
        {
            string messageId = reader.GetString(0);
            if (!attachments.TryGetValue(messageId, out var list))
            {
                list = [];
                attachments.Add(messageId, list);
            }
            list.Add(new Attachment(reader.GetString(1), reader.GetString(2), reader.GetString(3),
                (DocumentFormat)reader.GetInt32(4)));
        }
        return attachments;
    }
}
