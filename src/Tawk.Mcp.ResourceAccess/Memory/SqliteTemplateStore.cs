using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public sealed class SqliteTemplateStore(ISqliteConnectionFactory connections) : ITemplateStore
{
    private const string Columns = "SELECT name, description, category, voice, language, body, updated FROM response_template";

    public async Task<IReadOnlyList<ResponseTemplate>> ListAsync(string? category, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            // A category also matches the categories below it, so "family" lists family/spouse templates too.
            return await SqliteCommands.QueryAsync(
                connection,
                Columns + " WHERE $category IS NULL OR category = $category OR category LIKE $below ESCAPE '\\' ORDER BY name",
                Read,
                cancellationToken,
                ("$category", category),
                ("$below", category is null ? null : Like.Escape(category) + "/%")).ConfigureAwait(false);
        }
    }

    public async Task<ResponseTemplate?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var rows = await SqliteCommands.QueryAsync(connection, Columns + " WHERE name = $name", Read, cancellationToken, ("$name", name)).ConfigureAwait(false);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public async Task UpsertAsync(ResponseTemplate responseTemplate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(responseTemplate);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await SqliteCommands.ExecuteAsync(
                connection,
                "INSERT INTO response_template (name, description, category, voice, language, body, updated) "
                + "VALUES ($name, $description, $category, $voice, $language, $body, $updated) "
                + "ON CONFLICT (name) DO UPDATE SET description = excluded.description, category = excluded.category, "
                + "voice = excluded.voice, language = excluded.language, body = excluded.body, updated = excluded.updated",
                cancellationToken,
                null,
                ("$name", responseTemplate.Name),
                ("$description", responseTemplate.Description),
                ("$category", responseTemplate.Category),
                ("$voice", responseTemplate.Voice),
                ("$language", responseTemplate.Language),
                ("$body", responseTemplate.Body),
                ("$updated", SqliteCommands.ToUnixMs(responseTemplate.Updated))).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.ExecuteAsync(
                connection, "DELETE FROM response_template WHERE name = $name", cancellationToken, null, ("$name", name)).ConfigureAwait(false) > 0;
        }
    }

    private static ResponseTemplate Read(SqliteDataReader r) => new(
        r.GetString(0),
        SqliteCommands.NullableString(r, 1),
        SqliteCommands.NullableString(r, 2),
        SqliteCommands.NullableString(r, 3),
        SqliteCommands.NullableString(r, 4),
        r.GetString(5),
        SqliteCommands.FromUnixMs(r.GetInt64(6)));
}
