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
