using Microsoft.Data.Sqlite;

namespace Foxnet.Session;

internal sealed partial class SessionDatabase
{
    private static void CreateExpeditionExportSchema(SqliteConnection connection, SqliteTransaction transaction) =>
        Execute(connection, transaction, """
            CREATE TABLE expedition_evidence (
                value_id TEXT PRIMARY KEY REFERENCES registered_values(id),
                signal TEXT
            );
            CREATE TABLE orbital_cargo (
                value_id TEXT PRIMARY KEY REFERENCES registered_values(id),
                title TEXT NOT NULL,
                weight INTEGER NOT NULL CHECK(weight > 0),
                condition TEXT NOT NULL,
                delivered_at INTEGER NOT NULL,
                payload BLOB NOT NULL,
                transferred_at INTEGER
            );
            CREATE TABLE evidence_publications (
                value_id TEXT PRIMARY KEY REFERENCES expedition_evidence(value_id),
                sent_at INTEGER NOT NULL,
                status INTEGER NOT NULL DEFAULT 0,
                published_at INTEGER
            );
            CREATE TABLE npc_observations (
                contact_id TEXT NOT NULL REFERENCES contacts(id),
                id TEXT NOT NULL,
                text TEXT NOT NULL,
                source TEXT NOT NULL,
                confidence INTEGER NOT NULL,
                PRIMARY KEY(contact_id, id)
            );
            """);
}
