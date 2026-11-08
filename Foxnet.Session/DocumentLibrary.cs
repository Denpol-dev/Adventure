using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed class DocumentLibrary
{
    public const int SizeLimit = 16 * 1024 * 1024;
    private readonly SessionDatabase _database;

    internal DocumentLibrary(SessionDatabase database) => _database = database;

    public bool Add(ReceivedDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.EventId) || string.IsNullOrWhiteSpace(document.Title) ||
            string.IsNullOrWhiteSpace(document.FileName) || string.IsNullOrWhiteSpace(document.Source) ||
            !Enum.IsDefined(document.Format) || document.Content.Length > SizeLimit)
            throw new ArgumentException("Документ должен содержать метаданные и занимать не более 16 МБ.");

        using var connection = _database.Open();
        return Execute(connection, null, """
            INSERT INTO documents(event_id, title, file_name, format, source, received_at, content)
            VALUES($id, $title, $file, $format, $source, $receivedAt, $content)
            ON CONFLICT(event_id) DO NOTHING;
            """, ("id", document.EventId), ("title", document.Title), ("file", document.FileName),
            ("format", (int)document.Format), ("source", document.Source), ("receivedAt", Now()),
            ("content", document.Content)) == 1;
    }

    public LibraryDocument? AddFromAttachment(string eventId)
    {
        using var connection = _database.Open();
        Execute(connection, null, """
            INSERT INTO documents(event_id, title, file_name, format, source, received_at, content)
            SELECT a.event_id, a.title, a.file_name, a.format, 'Сообщение от ' || c.nickname, $receivedAt, a.content
            FROM attachments a JOIN messages m ON m.id = a.message_id JOIN contacts c ON c.id = m.contact_id
            WHERE a.event_id = $id ON CONFLICT(event_id) DO NOTHING;
            """, ("id", eventId), ("receivedAt", Now()));
        return Get(eventId);
    }

    public IReadOnlyList<DocumentInfo> List()
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, """
            SELECT event_id, title, file_name, format, source, received_at FROM documents ORDER BY rowid DESC;
            """);
        using var reader = query.ExecuteReader();
        var documents = new List<DocumentInfo>();
        while (reader.Read())
            documents.Add(ReadInfo(reader));
        return documents;
    }

    public LibraryDocument? Get(string eventId)
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, """
            SELECT event_id, title, file_name, format, source, received_at, content FROM documents WHERE event_id = $id;
            """, ("id", eventId));
        using var reader = query.ExecuteReader();
        return reader.Read() ? new LibraryDocument(ReadInfo(reader), reader.GetFieldValue<byte[]>(6)) : null;
    }

    private static DocumentInfo ReadInfo(SqliteDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), reader.GetString(2), (DocumentFormat)reader.GetInt32(3),
            reader.GetString(4), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)));
}
