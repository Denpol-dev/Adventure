using Microsoft.Data.Sqlite;

namespace Foxnet.Session;

internal sealed partial class SessionDatabase(string path)
{
    private const int SchemaVersion = 4;
    private readonly string _path = Path.GetFullPath(path);

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public SqliteConnection OpenForInitialization()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var connection = Open();
        try
        {
            using var check = Command(connection, null, "PRAGMA quick_check;");
            if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
                throw new InvalidDataException("Проверка целостности SQLite не прошла.");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public static void Migrate(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var query = Command(connection, transaction, "PRAGMA user_version;");
        var version = (long)query.ExecuteScalar()!;
        if (version == SchemaVersion)
            return;
        if (version is < 0 or > SchemaVersion)
            throw new InvalidDataException($"Версия локальной сессии {version} не поддерживается.");

        if (version == 0)
            CreateInitialSchema(connection, transaction);

        if (version < 2)
            Execute(connection, transaction, """
            CREATE TABLE player_account (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1) REFERENCES profile(singleton),
                salt BLOB NOT NULL CHECK(length(salt) = 16),
                password_hash BLOB NOT NULL CHECK(length(password_hash) = 32),
                failed_attempts INTEGER NOT NULL DEFAULT 0 CHECK(failed_attempts BETWEEN 0 AND 3)
            );
            """);

        if (version < 3)
            Execute(connection, transaction, """
            CREATE TABLE expeditions (
                world_id TEXT PRIMARY KEY NOT NULL,
                revision INTEGER NOT NULL CHECK(revision >= 1),
                payload BLOB NOT NULL
            );
            """);

        CreateWorldSchema(connection, transaction);
        Execute(connection, transaction, "PRAGMA user_version = 4;");
    }

    private static void CreateInitialSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        Execute(connection, transaction, """
            CREATE TABLE profile (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                id TEXT NOT NULL,
                nickname TEXT NOT NULL,
                avatar BLOB
            );
            CREATE TABLE contacts (
                id TEXT PRIMARY KEY NOT NULL,
                nickname TEXT NOT NULL
            );
            CREATE TABLE devices (
                id TEXT PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                kind TEXT NOT NULL,
                available INTEGER NOT NULL,
                authorized INTEGER NOT NULL DEFAULT 0,
                pid INTEGER,
                started_at INTEGER,
                CHECK((pid IS NULL) = (started_at IS NULL))
            );
            CREATE TABLE messages (
                id TEXT PRIMARY KEY NOT NULL,
                contact_id TEXT NOT NULL REFERENCES contacts(id),
                text TEXT NOT NULL,
                sent_at INTEGER NOT NULL,
                outgoing INTEGER NOT NULL,
                is_read INTEGER NOT NULL
            );
            CREATE INDEX messages_by_contact ON messages(contact_id);
            CREATE TABLE attachments (
                event_id TEXT PRIMARY KEY NOT NULL,
                message_id TEXT NOT NULL REFERENCES messages(id),
                title TEXT NOT NULL,
                file_name TEXT NOT NULL,
                format INTEGER NOT NULL,
                content BLOB NOT NULL
            );
            CREATE INDEX attachments_by_message ON attachments(message_id);
            CREATE TABLE selected_replies (
                message_id TEXT PRIMARY KEY NOT NULL REFERENCES messages(id),
                option_id TEXT NOT NULL
            );
            CREATE TABLE documents (
                event_id TEXT PRIMARY KEY NOT NULL,
                title TEXT NOT NULL,
                file_name TEXT NOT NULL,
                format INTEGER NOT NULL,
                source TEXT NOT NULL,
                received_at INTEGER NOT NULL,
                content BLOB NOT NULL
            );
            """);
    }

    public static int Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        return command.ExecuteNonQuery();
    }

    public static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue("$" + name, value ?? DBNull.Value);
        return command;
    }

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
