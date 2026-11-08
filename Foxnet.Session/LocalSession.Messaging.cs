using Microsoft.Data.Sqlite;
using System.Text;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed partial class LocalSession
{
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

    public IReadOnlyList<Message> OpenConversation(string contactId)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, "UPDATE messages SET is_read = 1 WHERE contact_id = $id AND is_read = 0;",
            ("id", contactId));

        var attachments = ReadAttachments(connection, transaction, contactId);
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
            var replies = reader.IsDBNull(4) && _messages.TryGetValue(id, out var definition)
                ? definition.Replies.Select(option => new ReplyOption(id, option.Id, option.Text)).ToArray()
                : [];
            messages.Add(new Message(id, contactId, reader.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)),
                reader.GetBoolean(3), attachments.GetValueOrDefault(id) ?? [], replies));
        }
        reader.Close();
        transaction.Commit();
        return messages;
    }

    public bool SelectReply(string messageId, string optionId)
    {
        if (!_messages.TryGetValue(messageId, out var message) ||
            message.Replies.Find(reply => reply.Id == optionId) is not ReplyDefinition option)
            return false;

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        if (Execute(connection, transaction, """
            INSERT INTO selected_replies(message_id, option_id)
            SELECT id, $option FROM messages WHERE id = $message AND outgoing = 0
            ON CONFLICT(message_id) DO NOTHING;
            """, ("message", messageId), ("option", optionId)) == 0)
            return false;

        Execute(connection, transaction, "UPDATE messages SET is_read = 1 WHERE id = $id;", ("id", messageId));

        Execute(connection, transaction, """
            INSERT INTO messages(id, contact_id, text, sent_at, outgoing, is_read)
            VALUES($id, $contact, $text, $sentAt, 1, 1);
            """, ("id", Guid.NewGuid().ToString("D")), ("contact", message.ContactId),
            ("text", option.Text), ("sentAt", Now()));

        if (option.NextMessageId is string nextId)
            Deliver(connection, transaction, _messages[nextId]);
        if (option.UnlockDeviceId is string deviceId)
            Execute(connection, transaction, "UPDATE devices SET available = 1 WHERE id = $id;", ("id", deviceId));

        transaction.Commit();
        return true;
    }

    private void Deliver(SqliteConnection connection, SqliteTransaction transaction, IncomingDefinition message)
    {
        int added = Execute(connection, transaction, """
            INSERT INTO messages(id, contact_id, text, sent_at, outgoing, is_read)
            VALUES($id, $contact, $text, $sentAt, 0, 0) ON CONFLICT(id) DO NOTHING;
            """, ("id", message.Id), ("contact", message.ContactId), ("text", _scenario.Render(message.Text, message)),
            ("sentAt", Now()));

        if (added == 0)
            return;
        foreach (var attachment in message.Attachments)
        {
            Execute(connection, transaction, """
                INSERT INTO attachments(event_id, message_id, title, file_name, format, content)
                VALUES($eventId, $messageId, $title, $fileName, $format, $content);
                """, ("eventId", attachment.EventId), ("messageId", message.Id), ("title", attachment.Title),
                ("fileName", attachment.FileName), ("format", (int)attachment.Format),
                ("content", Encoding.UTF8.GetBytes(_scenario.Render(attachment.Text, message))));
        }
    }

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
