using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed record ExpeditionSave(long Revision, byte[] Payload);

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

    public long Save(Guid worldId, long expectedRevision, byte[] payload)
    {
        ObjectDisposedException.ThrowIf(_lease.SafeFileHandle.IsClosed, this);
        using var connection = _database.Open();
        long revision = checked(expectedRevision + 1);
        int changed = expectedRevision == 0
            ? Execute(connection, null, """
                INSERT INTO expeditions(world_id, revision, payload) VALUES($id, $revision, $payload)
                ON CONFLICT(world_id) DO NOTHING;
                """, ("id", worldId.ToString("D")), ("revision", revision), ("payload", payload))
            : Execute(connection, null, """
                UPDATE expeditions SET revision = $revision, payload = $payload
                WHERE world_id = $id AND revision = $previous;
                """, ("id", worldId.ToString("D")), ("revision", revision), ("payload", payload), ("previous", expectedRevision));
        if (changed != 1)
            throw new InvalidOperationException("Ревизия экспедиции изменилась. Откройте консоль заново.");
        return revision;
    }

    public void Dispose() => _lease.Dispose();
}
