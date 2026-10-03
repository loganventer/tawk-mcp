using Microsoft.Data.Sqlite;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>Applies numbered schema steps in order and records progress in PRAGMA user_version.</summary>
public sealed class SqliteSchemaMigrator : ISchemaMigrator
{
    // Each step ends by setting user_version, inside the same transaction, so a step runs once or not at all.
    private static readonly string[] Steps =
    [
        """
        CREATE TABLE category (path TEXT PRIMARY KEY, description TEXT);
        CREATE TABLE voice (
            name TEXT PRIMARY KEY, description TEXT, guide TEXT NOT NULL, rules TEXT NOT NULL,
            is_default INTEGER NOT NULL DEFAULT 0, updated INTEGER NOT NULL);
        CREATE TABLE voice_variant (
            voice TEXT NOT NULL REFERENCES voice(name) ON DELETE CASCADE, category TEXT NOT NULL,
            guide TEXT NOT NULL, rules TEXT NOT NULL, examples TEXT NOT NULL, updated INTEGER NOT NULL,
            PRIMARY KEY (voice, category));
        CREATE TABLE contact (jid TEXT PRIMARY KEY, display_name TEXT, voice TEXT, updated INTEGER NOT NULL);
        CREATE TABLE contact_category (
            jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE, category TEXT NOT NULL,
            PRIMARY KEY (jid, category));
        CREATE TABLE contact_fact (
            jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE, field TEXT NOT NULL, value TEXT NOT NULL,
            source TEXT NOT NULL, confidence REAL NOT NULL, evidence TEXT, sensitive INTEGER NOT NULL,
            updated INTEGER NOT NULL, expires INTEGER, PRIMARY KEY (jid, field));
        CREATE TABLE contact_note (
            id INTEGER PRIMARY KEY AUTOINCREMENT, jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE,
            text TEXT NOT NULL, source TEXT NOT NULL, created INTEGER NOT NULL);
        CREATE TABLE response_template (
            name TEXT PRIMARY KEY, description TEXT, category TEXT, voice TEXT, language TEXT,
            body TEXT NOT NULL, updated INTEGER NOT NULL);
        PRAGMA user_version = 1;
        """,

        // Step 2: knowledge as Open Knowledge Format 0.2 concepts and links, held as rows. Contacts get a
        // concept each and notes become observation concepts linked to their contact; an observation's id
        // comes from its contact and creation time, so the same note has the same id on every machine.
        // Profile fields stay in contact_fact. sync_tombstone records deletes for the memory sync, which also
        // needs to know when a category or a contact's membership of one last changed.
        """
        CREATE TABLE okf_concept (
            id TEXT PRIMARY KEY, type TEXT NOT NULL, title TEXT, description TEXT, resource TEXT,
            tags TEXT NOT NULL, generated_by TEXT NOT NULL, generated_at INTEGER NOT NULL, verified TEXT NOT NULL,
            status TEXT NOT NULL, stale_after INTEGER, sources TEXT NOT NULL, extra TEXT NOT NULL,
            body TEXT NOT NULL, updated INTEGER NOT NULL);
        CREATE INDEX okf_concept_type ON okf_concept (type);
        CREATE TABLE okf_link (
            from_id TEXT NOT NULL REFERENCES okf_concept(id) ON DELETE CASCADE, to_id TEXT NOT NULL,
            label TEXT NOT NULL, note TEXT, source TEXT NOT NULL, confidence REAL NOT NULL, updated INTEGER NOT NULL,
            PRIMARY KEY (from_id, to_id, label));
        CREATE INDEX okf_link_to ON okf_link (to_id);
        CREATE TABLE sync_tombstone (
            kind TEXT NOT NULL, key TEXT NOT NULL, deleted INTEGER NOT NULL, PRIMARY KEY (kind, key));
        ALTER TABLE category ADD COLUMN updated INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE contact_category ADD COLUMN updated INTEGER NOT NULL DEFAULT 0;

        INSERT INTO okf_concept
            (id, type, title, description, resource, tags, generated_by, generated_at, verified, status,
             stale_after, sources, extra, body, updated)
        SELECT 'contacts/' || jid, 'Contact', display_name, NULL, 'whatsapp:' || jid, '[]', 'tawk-mcp/0.1.0', updated,
               '[]', 'stable', NULL, '[]', '{}', '', updated
        FROM contact;

        INSERT OR IGNORE INTO okf_concept
            (id, type, title, description, resource, tags, generated_by, generated_at, verified, status,
             stale_after, sources, extra, body, updated)
        SELECT 'observations/' || jid || '-' || created, 'Observation', NULL, NULL, NULL, '["general"]',
               CASE source WHEN 'user' THEN 'human:self' WHEN 'contact' THEN 'human:' || jid
                           WHEN 'imported' THEN 'process:import' ELSE 'tawk-mcp/0.1.0' END,
               created,
               CASE source WHEN 'user'
                    THEN '[{"by":"human:self","at":"' || strftime('%Y-%m-%dT%H:%M:%fZ', created / 1000.0, 'unixepoch') || '"}]'
                    ELSE '[]' END,
               'stable', NULL, '[]', '{"source":"' || source || '"}', text, created
        FROM contact_note;

        INSERT OR IGNORE INTO okf_link (from_id, to_id, label, note, source, confidence, updated)
        SELECT 'observations/' || jid || '-' || created, 'contacts/' || jid, 'about', NULL, source, 1.0, created
        FROM contact_note;

        ALTER TABLE contact_note RENAME TO contact_note_v1_backup;
        PRAGMA user_version = 2;
        """,

        // Step 3: which of the user's accounts a profile field or a relation was learnt through, as that
        // account's jid. Keys stay as they are, so a person known on two numbers is still one profile.
        // An observation keeps its tag among its producer keys, in extra. Rows from before are left untagged.
        """
        ALTER TABLE contact_fact ADD COLUMN account TEXT;
        ALTER TABLE okf_link ADD COLUMN account TEXT;
        PRAGMA user_version = 3;
        """,
    ];

    public static int CurrentVersion => Steps.Length;

    public async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var version = Convert.ToInt32(
            await SqliteCommands.ScalarAsync(connection, "PRAGMA user_version;", cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);

        for (var step = version; step < Steps.Length; step++)
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await SqliteCommands.ExecuteAsync(connection, Steps[step], cancellationToken, transaction).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
