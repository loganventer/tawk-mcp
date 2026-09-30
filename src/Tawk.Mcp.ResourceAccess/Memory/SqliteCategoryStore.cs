using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public sealed class SqliteCategoryStore(ISqliteConnectionFactory connections) : ICategoryStore
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
                "INSERT INTO category (path, description) VALUES ($path, $description) "
                + "ON CONFLICT (path) DO UPDATE SET description = excluded.description",
                cancellationToken,
                null,
                ("$path", category.Path),
                ("$description", category.Description)).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                var removed = await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM category WHERE path = $path", cancellationToken, transaction, ("$path", path)).ConfigureAwait(false);
                await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM voice_variant WHERE category = $path", cancellationToken, transaction, ("$path", path)).ConfigureAwait(false);
                await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM contact_category WHERE category = $path", cancellationToken, transaction, ("$path", path)).ConfigureAwait(false);
                await SqliteCommands.ExecuteAsync(
                    connection, "UPDATE response_template SET category = NULL WHERE category = $path", cancellationToken, transaction, ("$path", path)).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return removed > 0;
            }
        }
    }
}
