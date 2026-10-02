using Microsoft.Data.Sqlite;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>
/// Records deletes, so the memory sync can remove the same row on other machines instead of bringing
/// it back. Writing a row again clears its tombstone.
/// </summary>
internal static class Tombstones
{
    // A kind is the name of the table the row was in; a key is its primary key, parts joined by a line break.
    public const string Category = "category";
    public const string Voice = "voice";
    public const string VoiceVariant = "voice_variant";
    public const string Contact = "contact";
    public const string ContactCategory = "contact_category";
    public const string ContactFact = "contact_fact";
    public const string Template = "response_template";
    public const string Concept = "okf_concept";
    public const string Link = "okf_link";

    public static string Key(params string[] parts) => string.Join('\n', parts);

    public static string LinkKey(string fromId, string toId, string label) => Key(fromId, toId, label);

    public static Task MarkAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string kind, string key, DateTimeOffset now, CancellationToken cancellationToken) =>
        SqliteCommands.ExecuteAsync(
            connection,
            "INSERT INTO sync_tombstone (kind, key, deleted) VALUES ($kind, $key, $deleted) "
            + "ON CONFLICT (kind, key) DO UPDATE SET deleted = excluded.deleted",
            cancellationToken,
            transaction,
            ("$kind", kind),
            ("$key", key),
            ("$deleted", SqliteCommands.ToUnixMs(now)));

    public static Task ClearAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string kind, string key, CancellationToken cancellationToken) =>
        SqliteCommands.ExecuteAsync(
            connection,
            "DELETE FROM sync_tombstone WHERE kind = $kind AND key = $key",
            cancellationToken,
            transaction,
            ("$kind", kind),
            ("$key", key));
}
