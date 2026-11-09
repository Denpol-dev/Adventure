using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed partial class LocalSession
{
    public IReadOnlyList<ContactCard> GetContacts()
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, """
            SELECT c.id, c.nickname, s.blocked, w.contact_id IS NOT NULL,
                (SELECT COUNT(*) FROM messages WHERE contact_id = c.id AND is_read = 0), s.known_name
            FROM contact_state s JOIN contacts c ON c.id = s.contact_id
            LEFT JOIN npc_wallets w ON w.contact_id = c.id
            WHERE s.known = 1 ORDER BY c.nickname;
            """);
        using var reader = query.ExecuteReader();
        var result = new List<ContactCard>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var details = _contacts.GetValueOrDefault(id)?.Details;
            var name = reader.IsDBNull(5) ? details?.Name : reader.GetString(5);
            result.Add(new ContactCard(id, reader.GetString(1), name, details?.Age, details?.Organization,
                details?.Summary ?? "", details?.Avatar, reader.GetBoolean(2), reader.GetBoolean(3), reader.GetInt32(4)));
        }
        return result;
    }

    public bool RevealContactName(string contactId, string name, FactDefinition evidence)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя контакта отсутствует.", nameof(name));
        ValidateFact(evidence);

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var updated = Execute(connection, transaction, """
            UPDATE contact_state SET known_name = $name WHERE contact_id = $id AND known = 1;
            """, ("name", name), ("id", contactId)) == 1;
        if (updated)
            AddFact(connection, transaction, contactId, evidence);
        transaction.Commit();
        return updated;
    }

    public Dossier? GetDossier(string contactId)
    {
        var contact = GetContacts().FirstOrDefault(item => item.Id == contactId);
        if (contact == null)
            return null;

        using var connection = _database.Open();
        var notes = (string)Scalar(connection, null, "SELECT notes FROM contact_state WHERE contact_id = $id;", ("id", contactId))!;
        using var factsQuery = Command(connection, null, """
            SELECT id, text, source, confidence FROM contact_facts WHERE contact_id = $id ORDER BY rowid;
            """, ("id", contactId));
        var facts = new List<FactDefinition>();
        using (var reader = factsQuery.ExecuteReader())
        {
            while (reader.Read())
                facts.Add(new FactDefinition(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    (FactConfidence)reader.GetInt32(3)));
        }
        using var historyQuery = Command(connection, null, """
            SELECT text, happened_at FROM contact_history WHERE contact_id = $id ORDER BY happened_at DESC, rowid DESC;
            """, ("id", contactId));
        using var historyReader = historyQuery.ExecuteReader();
        var history = new List<ContactInteraction>();
        while (historyReader.Read())
            history.Add(new ContactInteraction(historyReader.GetString(0), At(historyReader.GetInt64(1))));
        return new Dossier(contact, notes, facts, history);
    }

    public bool SaveContactNotes(string contactId, string notes)
    {
        using var connection = _database.Open();
        return Execute(connection, null, "UPDATE contact_state SET notes = $notes WHERE contact_id = $id AND known = 1;",
            ("notes", notes), ("id", contactId)) == 1;
    }

    public bool RecordContactFact(string contactId, FactDefinition fact)
    {
        ValidateFact(fact);

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        bool known = Scalar(connection, transaction,
            "SELECT 1 FROM contact_state WHERE contact_id = $id AND known = 1;", ("id", contactId)) != null;
        if (known)
            AddFact(connection, transaction, contactId, fact);
        transaction.Commit();
        return known;
    }

    private static void ValidateFact(FactDefinition fact)
    {
        if (string.IsNullOrWhiteSpace(fact.Id) || string.IsNullOrWhiteSpace(fact.Text) ||
            string.IsNullOrWhiteSpace(fact.Source) || !Enum.IsDefined(fact.Confidence))
            throw new ArgumentException("Некорректное сведение о контакте.", nameof(fact));
    }

    private static object? Scalar(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var query = Command(connection, transaction, sql, parameters);
        return query.ExecuteScalar();
    }

    private static void AddFact(SqliteConnection connection, SqliteTransaction transaction, string contactId, FactDefinition fact) =>
        Execute(connection, transaction, """
            INSERT INTO contact_facts(contact_id, id, text, source, confidence) VALUES($contact, $id, $text, $source, $confidence)
            ON CONFLICT(contact_id, id) DO NOTHING;
            """, ("contact", contactId), ("id", fact.Id), ("text", fact.Text), ("source", fact.Source),
            ("confidence", (int)fact.Confidence));

    private static void AddHistory(SqliteConnection connection, SqliteTransaction transaction, string id, string contactId,
        string text, long time) =>
        Execute(connection, transaction, """
            INSERT INTO contact_history(id, contact_id, text, happened_at) VALUES($id, $contact, $text, $time)
            ON CONFLICT(id) DO NOTHING;
            """, ("id", id), ("contact", contactId), ("text", text), ("time", time));

    private static bool IsBlocked(SqliteConnection connection, SqliteTransaction transaction, string contactId) =>
        Scalar(connection, transaction, "SELECT blocked FROM contact_state WHERE contact_id = $id;", ("id", contactId)) is 1L;

    private static void Notify(SqliteConnection connection, SqliteTransaction transaction, string id, bool bank) =>
        Execute(connection, transaction, "INSERT INTO notifications(id, bank) VALUES($id, $bank) ON CONFLICT(id) DO NOTHING;",
            ("id", id), ("bank", bank));

    public NotificationState GetNotifications(bool readBank = false)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction(deferred: !readBank);
        if (readBank)
            Execute(connection, transaction, "UPDATE notifications SET seen = 1 WHERE bank = 1 AND seen = 0;");
        using var query = Command(connection, transaction, """
            SELECT (SELECT COALESCE(MAX(revision), 0) FROM notifications),
                   (SELECT COUNT(*) FROM messages WHERE is_read = 0),
                   (SELECT COUNT(*) FROM notifications WHERE bank = 1 AND seen = 0),
                   sound FROM world_settings WHERE singleton = 1;
            """);
        using var reader = query.ExecuteReader();
        reader.Read();
        var state = new NotificationState(reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetBoolean(3));
        reader.Close();
        transaction.Commit();
        return state;
    }

    public void SetNotificationSound(bool enabled)
    {
        using var connection = _database.Open();
        Execute(connection, null, "UPDATE world_settings SET sound = $enabled WHERE singleton = 1;", ("enabled", enabled));
    }
}
