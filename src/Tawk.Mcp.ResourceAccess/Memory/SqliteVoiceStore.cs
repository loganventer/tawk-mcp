using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public sealed class SqliteVoiceStore(ISqliteConnectionFactory connections) : IVoiceStore
{
    private const string VoiceColumns = "SELECT name, description, guide, rules, is_default, updated FROM voice";
    private const string VariantColumns = "SELECT voice, category, guide, rules, examples, updated FROM voice_variant";

    public async Task<IReadOnlyList<Voice>> ListAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(connection, VoiceColumns + " ORDER BY name", ReadVoice, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Voice?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var rows = await SqliteCommands.QueryAsync(
                connection, VoiceColumns + " WHERE name = $name", ReadVoice, cancellationToken, ("$name", name)).ConfigureAwait(false);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public async Task<Voice?> GetDefaultAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var rows = await SqliteCommands.QueryAsync(
                connection, VoiceColumns + " WHERE is_default = 1 LIMIT 1", ReadVoice, cancellationToken).ConfigureAwait(false);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public async Task UpsertAsync(Voice voice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                if (voice.IsDefault)
                {
                    await SqliteCommands.ExecuteAsync(connection, "UPDATE voice SET is_default = 0", cancellationToken, transaction).ConfigureAwait(false);
                }

                await SqliteCommands.ExecuteAsync(
                    connection,
                    "INSERT INTO voice (name, description, guide, rules, is_default, updated) "
                    + "VALUES ($name, $description, $guide, $rules, $isDefault, $updated) "
                    + "ON CONFLICT (name) DO UPDATE SET description = excluded.description, guide = excluded.guide, "
                    + "rules = excluded.rules, is_default = excluded.is_default, updated = excluded.updated",
                    cancellationToken,
                    transaction,
                    ("$name", voice.Name),
                    ("$description", voice.Description),
                    ("$guide", voice.Guide),
                    ("$rules", StoreJson.Write(voice.Rules)),
                    ("$isDefault", voice.IsDefault ? 1 : 0),
                    ("$updated", SqliteCommands.ToUnixMs(voice.Updated))).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.ExecuteAsync(
                connection, "DELETE FROM voice WHERE name = $name", cancellationToken, null, ("$name", name)).ConfigureAwait(false) > 0;
        }
    }

    public async Task<IReadOnlyList<VoiceVariant>> ListVariantsAsync(string voice, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection, VariantColumns + " WHERE voice = $voice ORDER BY category", ReadVariant, cancellationToken, ("$voice", voice)).ConfigureAwait(false);
        }
    }

    public async Task UpsertVariantAsync(VoiceVariant variant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variant);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await SqliteCommands.ExecuteAsync(
                connection,
                "INSERT INTO voice_variant (voice, category, guide, rules, examples, updated) "
                + "VALUES ($voice, $category, $guide, $rules, $examples, $updated) "
                + "ON CONFLICT (voice, category) DO UPDATE SET guide = excluded.guide, rules = excluded.rules, "
                + "examples = excluded.examples, updated = excluded.updated",
                cancellationToken,
                null,
                ("$voice", variant.Voice),
                ("$category", variant.Category),
                ("$guide", variant.Guide),
                ("$rules", StoreJson.Write(variant.Rules)),
                ("$examples", StoreJson.Write(variant.Examples)),
                ("$updated", SqliteCommands.ToUnixMs(variant.Updated))).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteVariantAsync(string voice, string category, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.ExecuteAsync(
                connection,
                "DELETE FROM voice_variant WHERE voice = $voice AND category = $category",
                cancellationToken,
                null,
                ("$voice", voice),
                ("$category", category)).ConfigureAwait(false) > 0;
        }
    }

    private static Voice ReadVoice(SqliteDataReader r) => new(
        r.GetString(0),
        SqliteCommands.NullableString(r, 1),
        r.GetString(2),
        StoreJson.Read(r.GetString(3), VoiceRules.None),
        r.GetInt64(4) == 1,
        SqliteCommands.FromUnixMs(r.GetInt64(5)));

    private static VoiceVariant ReadVariant(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        StoreJson.Read(r.GetString(3), VoiceRules.None),
        StoreJson.Read<IReadOnlyList<string>>(r.GetString(4), []),
        SqliteCommands.FromUnixMs(r.GetInt64(5)));
}
