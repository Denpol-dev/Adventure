using System.Text;
using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed record ExpeditionSave(long Revision, byte[] Payload);
public abstract record ExpeditionExport(string Id, long Appraisal);
public sealed record EvidenceExport(string Id, string Title, string Source, string Text, long Appraisal, string? Signal)
    : ExpeditionExport(Id, Appraisal);
public sealed record OrbitalCargoExport(string Id, string Title, int Weight, string Condition, long Appraisal, byte[] Payload)
    : ExpeditionExport(Id, Appraisal);

public sealed class ExpeditionStore : IDisposable
{
    private readonly SessionDatabase _database;
    private readonly FileStream _lease;

    public ExpeditionStore(string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            _lease = new FileStream(path + ".expedition.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new IOException("Экспедиция уже открыта другой консолью или файл блокировки недоступен.", exception);
        }

        _database = new SessionDatabase(path);
        try
        {
            using var connection = _database.OpenForInitialization();
            using var transaction = connection.BeginTransaction();
            Migrate(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            _lease.Dispose();
            throw;
        }
    }

    public ExpeditionSave? Load(Guid worldId)
    {
        ObjectDisposedException.ThrowIf(_lease.SafeFileHandle.IsClosed, this);
        using var connection = _database.Open();
        using var query = Command(connection, null, "SELECT revision, payload FROM expeditions WHERE world_id = $id;",
            ("id", worldId.ToString("D")));
        using var reader = query.ExecuteReader();
        return reader.Read() ? new ExpeditionSave(reader.GetInt64(0), reader.GetFieldValue<byte[]>(1)) : null;
    }

    public long Save(Guid worldId, long expectedRevision, byte[] payload, ExpeditionExport? export = null)
    {
        ObjectDisposedException.ThrowIf(_lease.SafeFileHandle.IsClosed, this);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        long revision = checked(expectedRevision + 1);
        int changed = expectedRevision == 0
            ? Execute(connection, transaction, """
                INSERT INTO expeditions(world_id, revision, payload) VALUES($id, $revision, $payload)
                ON CONFLICT(world_id) DO NOTHING;
                """, ("id", worldId.ToString("D")), ("revision", revision), ("payload", payload))
            : Execute(connection, transaction, """
                UPDATE expeditions SET revision = $revision, payload = $payload
                WHERE world_id = $id AND revision = $previous;
                """, ("id", worldId.ToString("D")), ("revision", revision), ("payload", payload), ("previous", expectedRevision));
        if (changed != 1)
            throw new InvalidOperationException("Ревизия экспедиции изменилась. Откройте консоль заново.");
        if (export != null)
            SaveExport(connection, transaction, worldId, export);
        transaction.Commit();
        return revision;
    }

    private static void SaveExport(SqliteConnection connection, SqliteTransaction transaction, Guid worldId,
        ExpeditionExport export)
    {
        string id = $"expedition:{worldId:N}:{export.Id}";
        long now = Now();
        if (export is EvidenceExport evidence)
        {
            Execute(connection, transaction, """
                INSERT INTO documents(event_id, title, file_name, format, source, received_at, content)
                VALUES($id, $title, $file, $format, $source, $now, $content);
                """, ("id", id), ("title", evidence.Title), ("file", evidence.Id + ".txt"),
                ("format", (int)DocumentFormat.Text), ("source", evidence.Source), ("now", now),
                ("content", Encoding.UTF8.GetBytes(evidence.Text)));
            Register(ValueKind.Document, id);
            Execute(connection, transaction, "INSERT INTO expedition_evidence(value_id, signal) VALUES($id, $signal);",
                ("id", id), ("signal", evidence.Signal));
        }
        else if (export is OrbitalCargoExport cargo)
        {
            Register(ValueKind.PhysicalItem, null);
            Execute(connection, transaction, """
                INSERT INTO orbital_cargo(value_id, title, weight, condition, delivered_at, payload)
                VALUES($id, $title, $weight, $condition, $now, $payload);
                """, ("id", id), ("title", cargo.Title), ("weight", cargo.Weight), ("condition", cargo.Condition),
                ("now", now), ("payload", cargo.Payload));
        }
        else
            throw new InvalidDataException("Неизвестная операция экспорта экспедиции.");

        Execute(connection, transaction, "INSERT INTO notifications(id, bank) VALUES($id, $bank);",
            ("id", "export:" + id), ("bank", export is OrbitalCargoExport));

        void Register(ValueKind kind, string? document) => Execute(connection, transaction, """
            INSERT INTO registered_values(id, kind, appraisal, document_id) VALUES($id, $kind, $appraisal, $document);
            """, ("id", id), ("kind", (int)kind), ("appraisal", export.Appraisal), ("document", document));
    }

    public void Dispose() => _lease.Dispose();
}
