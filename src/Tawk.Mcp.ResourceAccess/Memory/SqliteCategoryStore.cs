using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public sealed class SqliteCategoryStore(ISqliteConnectionFactory connections, TimeProvider clock) : ICategoryStore
{
    public async Task<IReadOnlyList<AudienceCategory>> ListAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection,
                "SELECT path, description FROM category ORDER BY path",
                r => new AudienceCategory(r.GetString(0), SqliteCommands.NullableString(r, 1)),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<AudienceCategory?> GetAsync(string path, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var rows = await SqliteCommands.QueryAsync(
                connection,
                "SELECT path, description FROM category WHERE path = $path",
                r => new AudienceCategory(r.GetString(0), SqliteCommands.NullableString(r, 1)),
                cancellationToken,
                ("$path", path)).ConfigureAwait(false);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public async Task UpsertAsync(AudienceCategory category, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(category);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await SqliteCommands.ExecuteAsync(
                connection,
                "INSERT INTO category (path, description, updated) VALUES ($path, $description, $updated) "
                + "ON CONFLICT (path) DO UPDATE SET description = excluded.description, updated = excluded.updated",
                cancellationToken,
                null,
                ("$path", category.Path),
                ("$description", category.Description),
                ("$updated", SqliteCommands.ToUnixMs(clock.GetUtcNow()))).ConfigureAwait(false);
            await Tombstones.ClearAsync(connection, null, Tombstones.Category, category.Path, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Removes a category and what hangs on it, recording each delete for the memory sync.</summary>
    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var variants = await SqliteCommands.QueryAsync(
                connection, "SELECT voice FROM voice_variant WHERE category = $path", r => r.GetString(0), cancellationToken, ("$path", path)).ConfigureAwait(false);
            var members = await SqliteCommands.QueryAsync(
                connection, "SELECT jid FROM contact_category WHERE category = $path", r => r.GetString(0), cancellationToken, ("$path", path)).ConfigureAwait(false);
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                foreach (var voice in variants)
                {
                    await Tombstones.MarkAsync(connection, transaction, Tombstones.VoiceVariant, Tombstones.Key(voice, path), now, cancellationToken).ConfigureAwait(false);
                }

                foreach (var jid in members)
                {
                    await Tombstones.MarkAsync(connection, transaction, Tombstones.ContactCategory, Tombstones.Key(jid, path), now, cancellationToken).ConfigureAwait(false);
                }

                var removed = await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM category WHERE path = $path", cancellationToken, transaction, ("$path", path)).ConfigureAwait(false);
                await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM voice_variant WHERE category = $path", cancellationToken, transaction, ("$path", path)).ConfigureAwait(false);
                await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM contact_category WHERE category = $path", cancellationToken, transaction, ("$path", path)).ConfigureAwait(false);
                await SqliteCommands.ExecuteAsync(
                    connection,
                    "UPDATE response_template SET category = NULL, updated = $now WHERE category = $path",
                    cancellationToken,
                    transaction,
                    ("$path", path),
                    ("$now", SqliteCommands.ToUnixMs(now))).ConfigureAwait(false);
                if (removed > 0)
                {
                    await Tombstones.MarkAsync(connection, transaction, Tombstones.Category, path, now, cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return removed > 0;
            }
        }
    }
}
