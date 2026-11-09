using Microsoft.Data.Sqlite;

namespace Foxnet.Session;

internal sealed partial class SessionDatabase
{
    private static void CreateWorldSchema(SqliteConnection connection, SqliteTransaction transaction) =>
        Execute(connection, transaction, """
            CREATE TABLE contact_state (
                contact_id TEXT PRIMARY KEY NOT NULL REFERENCES contacts(id),
                known INTEGER NOT NULL DEFAULT 0,
                blocked INTEGER NOT NULL DEFAULT 0,
                known_name TEXT,
                notes TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE contact_facts (
                contact_id TEXT NOT NULL REFERENCES contacts(id),
                id TEXT NOT NULL,
                text TEXT NOT NULL,
                source TEXT NOT NULL,
                confidence INTEGER NOT NULL,
                PRIMARY KEY(contact_id, id)
            );
            CREATE TABLE contact_history (
                id TEXT PRIMARY KEY NOT NULL,
                contact_id TEXT NOT NULL REFERENCES contacts(id),
                text TEXT NOT NULL,
                happened_at INTEGER NOT NULL
            );
            CREATE INDEX history_by_contact ON contact_history(contact_id);
            CREATE TABLE independent_replies (
                message_id TEXT NOT NULL REFERENCES messages(id),
                option_id TEXT NOT NULL,
                PRIMARY KEY(message_id, option_id)
            );
            CREATE TABLE story_flags (
                key TEXT PRIMARY KEY NOT NULL,
                value TEXT NOT NULL
            );
            CREATE TABLE story_events (
                id TEXT PRIMARY KEY NOT NULL,
                kind TEXT NOT NULL,
                subject TEXT NOT NULL,
                due_at INTEGER NOT NULL,
                prerequisite TEXT REFERENCES story_events(id),
                done INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX pending_story ON story_events(due_at) WHERE done = 0;
            CREATE TABLE player_wallet (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                balance INTEGER NOT NULL CHECK(typeof(balance) = 'integer' AND balance >= 0)
            );
            CREATE TABLE npc_wallets (
                contact_id TEXT PRIMARY KEY NOT NULL REFERENCES contacts(id),
                balance INTEGER NOT NULL CHECK(typeof(balance) = 'integer' AND balance >= 0),
                credits_per_minute INTEGER NOT NULL CHECK(credits_per_minute >= 0),
                last_accrual INTEGER NOT NULL
            );
            CREATE TABLE bank_entries (
                id TEXT PRIMARY KEY NOT NULL,
                contact_id TEXT REFERENCES contacts(id),
                amount INTEGER NOT NULL,
                purpose TEXT NOT NULL,
                happened_at INTEGER NOT NULL
            );
            CREATE TABLE npc_ledger (
                id TEXT PRIMARY KEY NOT NULL,
                contact_id TEXT NOT NULL REFERENCES npc_wallets(contact_id),
                amount INTEGER NOT NULL,
                kind TEXT NOT NULL,
                happened_at INTEGER NOT NULL
            );
            CREATE TABLE contract_payments (
                id TEXT PRIMARY KEY NOT NULL,
                contact_id TEXT NOT NULL REFERENCES npc_wallets(contact_id),
                amount INTEGER NOT NULL CHECK(amount > 0),
                purpose TEXT NOT NULL,
                due_at INTEGER NOT NULL,
                status INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE payment_requests (
                id TEXT PRIMARY KEY NOT NULL,
                contact_id TEXT NOT NULL REFERENCES contacts(id),
                amount INTEGER NOT NULL CHECK(amount > 0),
                purpose TEXT NOT NULL,
                incoming INTEGER NOT NULL,
                status INTEGER NOT NULL,
                created_at INTEGER NOT NULL,
                expires_at INTEGER
            );
            CREATE UNIQUE INDEX active_request_per_contact
                ON payment_requests(contact_id) WHERE status = 0 AND incoming = 0;
            CREATE INDEX outgoing_request_history ON payment_requests(contact_id, created_at) WHERE incoming = 0;
            CREATE TABLE registered_values (
                id TEXT PRIMARY KEY NOT NULL,
                kind INTEGER NOT NULL,
                appraisal INTEGER NOT NULL CHECK(appraisal > 0),
                document_id TEXT UNIQUE REFERENCES documents(event_id),
                owned INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE value_sales (
                id TEXT PRIMARY KEY NOT NULL,
                value_id TEXT NOT NULL REFERENCES registered_values(id),
                buyer_id TEXT NOT NULL REFERENCES contacts(id),
                appraisal INTEGER NOT NULL,
                paid INTEGER NOT NULL,
                multiplier INTEGER NOT NULL,
                proceeds INTEGER NOT NULL,
                settled_at INTEGER NOT NULL,
                UNIQUE(value_id, buyer_id)
            );
            CREATE TABLE notifications (
                revision INTEGER PRIMARY KEY,
                id TEXT NOT NULL UNIQUE,
                bank INTEGER NOT NULL,
                seen INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE world_settings (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                sound INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE corporate_pressure (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                evidence INTEGER NOT NULL DEFAULT 0,
                risk INTEGER NOT NULL DEFAULT 0,
                demand INTEGER NOT NULL DEFAULT 0,
                irritation INTEGER NOT NULL DEFAULT 0,
                trust INTEGER NOT NULL DEFAULT 0
            );
            INSERT INTO contact_state(contact_id, known)
            SELECT DISTINCT contact_id, 1 FROM messages;
            INSERT INTO world_settings(singleton) VALUES(1);
            """);
}
