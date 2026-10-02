using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.ResourceAccess.Memory;

public sealed class SqliteOkfStore(ISqliteConnectionFactory connections, TimeProvider clock) : IOkfStore
{
    public async Task<OkfConcept?> GetAsync(string id, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var rows = await SqliteCommands.QueryAsync(
                connection, OkfRows.ConceptColumns + " WHERE c.id = $id", OkfRows.ReadConcept, cancellationToken, ("$id", id)).ConfigureAwait(false);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public async Task<IReadOnlyList<OkfConcept>> ListAsync(
        string? type, string? linkedTo, string? tag, string? query, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection,
                OkfRows.ConceptColumns
                + " WHERE ($type IS NULL OR c.type = $type)"
                + " AND ($to IS NULL OR EXISTS (SELECT 1 FROM okf_link l WHERE l.from_id = c.id AND l.to_id = $to))"
                + " AND ($tag IS NULL OR EXISTS (SELECT 1 FROM json_each(c.tags) t WHERE t.value = $tag))"
                + " AND ($query IS NULL OR c.title LIKE $like ESCAPE '\\' OR c.description LIKE $like ESCAPE '\\'"
                + " OR c.body LIKE $like ESCAPE '\\')"
                + " ORDER BY c.generated_at DESC, c.id",
                OkfRows.ReadConcept,
                cancellationToken,
                ("$type", type),
                ("$to", linkedTo),
                ("$tag", tag),
                ("$query", query),
                ("$like", query is null ? null : "%" + Like.Escape(query) + "%")).ConfigureAwait(false);
        }
    }

    public async Task UpsertAsync(OkfConcept concept, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concept);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await OkfRows.UpsertConceptAsync(connection, null, concept, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                var removed = await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM okf_concept WHERE id = $id", cancellationToken, transaction, ("$id", id)).ConfigureAwait(false) > 0;
                if (removed)
                {
                    await Tombstones.MarkAsync(connection, transaction, Tombstones.Concept, id, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return removed;
            }
        }
    }

    public async Task<IReadOnlyList<OkfLink>> GetLinksFromAsync(string id, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection, OkfRows.LinkColumns + " WHERE from_id = $id ORDER BY label, to_id", OkfRows.ReadLink, cancellationToken, ("$id", id)).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<OkfLink>> GetLinksToAsync(string id, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection, OkfRows.LinkColumns + " WHERE to_id = $id ORDER BY label, from_id", OkfRows.ReadLink, cancellationToken, ("$id", id)).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<OkfLink>> ListLinksAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection, OkfRows.LinkColumns + " ORDER BY from_id, label, to_id", OkfRows.ReadLink, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UpsertLinkAsync(OkfLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await OkfRows.UpsertLinkAsync(connection, null, link, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteLinkAsync(string fromId, string toId, string label, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                var removed = await SqliteCommands.ExecuteAsync(
                    connection,
                    "DELETE FROM okf_link WHERE from_id = $from AND to_id = $to AND label = $label",
                    cancellationToken,
                    transaction,
                    ("$from", fromId),
                    ("$to", toId),
                    ("$label", label)).ConfigureAwait(false) > 0;
                if (removed)
                {
                    await Tombstones.MarkAsync(
                        connection, transaction, Tombstones.Link, Tombstones.LinkKey(fromId, toId, label), clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return removed;
            }
        }
    }
}
