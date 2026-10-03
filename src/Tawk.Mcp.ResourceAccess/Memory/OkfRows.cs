using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>How concepts and links are written to and read from their tables, shared by the stores that use them.</summary>
internal static class OkfRows
{
    public const string ConceptColumns =
        "SELECT c.id, c.type, c.title, c.description, c.resource, c.tags, c.generated_by, c.generated_at, c.verified, "
        + "c.status, c.stale_after, c.sources, c.extra, c.body, c.updated FROM okf_concept c";

    public const string LinkColumns = "SELECT from_id, to_id, label, note, source, confidence, updated, account FROM okf_link";

    private const string UpsertConceptSql =
        "INSERT INTO okf_concept (id, type, title, description, resource, tags, generated_by, generated_at, verified, status, "
        + "stale_after, sources, extra, body, updated) VALUES ($id, $type, $title, $description, $resource, $tags, $by, $at, "
        + "$verified, $status, $stale, $sources, $extra, $body, $updated) "
        + "ON CONFLICT (id) DO UPDATE SET type = excluded.type, title = excluded.title, description = excluded.description, "
        + "resource = excluded.resource, tags = excluded.tags, generated_by = excluded.generated_by, "
        + "generated_at = excluded.generated_at, verified = excluded.verified, status = excluded.status, "
        + "stale_after = excluded.stale_after, sources = excluded.sources, extra = excluded.extra, body = excluded.body, "
        + "updated = excluded.updated";

    private const string UpsertLinkSql =
        "INSERT INTO okf_link (from_id, to_id, label, note, source, confidence, updated, account) "
        + "VALUES ($from, $to, $label, $note, $source, $confidence, $updated, $account) "
        + "ON CONFLICT (from_id, to_id, label) DO UPDATE SET note = excluded.note, source = excluded.source, "
        + "confidence = excluded.confidence, updated = excluded.updated, account = excluded.account";

    public static async Task UpsertConceptAsync(
        SqliteConnection connection, SqliteTransaction? transaction, OkfConcept concept, CancellationToken cancellationToken)
    {
        await SqliteCommands.ExecuteAsync(
            connection,
            UpsertConceptSql,
            cancellationToken,
            transaction,
            ("$id", concept.Id),
            ("$type", concept.Type),
            ("$title", concept.Title),
            ("$description", concept.Description),
            ("$resource", concept.Resource),
            ("$tags", StoreJson.Write(concept.Tags)),
            ("$by", concept.GeneratedBy),
            ("$at", SqliteCommands.ToUnixMs(concept.GeneratedAt)),
            ("$verified", StoreJson.Write(concept.Verified)),
            ("$status", StatusName(concept.Status)),
            ("$stale", concept.StaleAfter is { } stale ? SqliteCommands.ToUnixMs(stale) : null),
            ("$sources", StoreJson.Write(concept.Sources)),
            ("$extra", string.IsNullOrWhiteSpace(concept.ExtraJson) ? "{}" : concept.ExtraJson),
            ("$body", concept.Body),
            ("$updated", SqliteCommands.ToUnixMs(concept.Updated))).ConfigureAwait(false);
        await Tombstones.ClearAsync(connection, transaction, Tombstones.Concept, concept.Id, cancellationToken).ConfigureAwait(false);
    }

    public static async Task UpsertLinkAsync(
        SqliteConnection connection, SqliteTransaction? transaction, OkfLink link, CancellationToken cancellationToken)
    {
        await SqliteCommands.ExecuteAsync(
            connection,
            UpsertLinkSql,
            cancellationToken,
            transaction,
            ("$from", link.FromId),
            ("$to", link.ToId),
            ("$label", link.Label),
            ("$note", link.Note),
            ("$source", SourceNames.ToName(link.Source)),
            ("$confidence", link.Confidence),
            ("$updated", SqliteCommands.ToUnixMs(link.Updated)),
            ("$account", link.Account)).ConfigureAwait(false);
        await Tombstones.ClearAsync(
            connection, transaction, Tombstones.Link, Tombstones.LinkKey(link.FromId, link.ToId, link.Label), cancellationToken).ConfigureAwait(false);
    }

    public static OkfConcept ReadConcept(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        SqliteCommands.NullableString(r, 2),
        SqliteCommands.NullableString(r, 3),
        SqliteCommands.NullableString(r, 4),
        StoreJson.Read<List<string>>(r.GetString(5), []),
        r.GetString(6),
        SqliteCommands.FromUnixMs(r.GetInt64(7)),
        StoreJson.Read<List<OkfVerification>>(r.GetString(8), []),
        StatusFromName(r.GetString(9)),
        r.IsDBNull(10) ? null : SqliteCommands.FromUnixMs(r.GetInt64(10)),
        StoreJson.Read<List<OkfSource>>(r.GetString(11), []),
        r.GetString(12),
        r.GetString(13),
        SqliteCommands.FromUnixMs(r.GetInt64(14)));

    public static OkfLink ReadLink(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        SqliteCommands.NullableString(r, 3),
        SourceNames.FromName(r.GetString(4)),
        r.GetDouble(5),
        SqliteCommands.FromUnixMs(r.GetInt64(6)))
    {
        Account = SqliteCommands.NullableString(r, 7),
    };

    public static string StatusName(OkfStatus status) => status switch
    {
        OkfStatus.Draft => "draft",
        OkfStatus.Deprecated => "deprecated",
        _ => "stable",
    };

    public static OkfStatus StatusFromName(string name) => name switch
    {
        "draft" => OkfStatus.Draft,
        "deprecated" => OkfStatus.Deprecated,
        _ => OkfStatus.Stable,
    };
}
