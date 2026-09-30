using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public sealed class SqliteContactStore(ISqliteConnectionFactory connections) : IContactStore
{
    private const string ContactColumns = "SELECT c.jid, c.display_name, c.voice, c.updated FROM contact c";
    private const string FactColumns = "SELECT jid, field, value, source, confidence, evidence, sensitive, updated, expires FROM contact_fact";

    public async Task<ContactRecord?> GetAsync(string jid, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var rows = await SqliteCommands.QueryAsync(
                connection, ContactColumns + " WHERE c.jid = $jid", ReadContact, cancellationToken, ("$jid", jid)).ConfigureAwait(false);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    public async Task<IReadOnlyList<ContactRecord>> ListAsync(string? category, string? query, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection,
                ContactColumns
                + " WHERE ($category IS NULL OR EXISTS (SELECT 1 FROM contact_category cc WHERE cc.jid = c.jid"
                + " AND (cc.category = $category OR cc.category LIKE $below ESCAPE '\\')))"
                + " AND ($query IS NULL OR c.jid LIKE $like ESCAPE '\\' OR c.display_name LIKE $like ESCAPE '\\')"
                + " ORDER BY COALESCE(c.display_name, c.jid)",
                ReadContact,
                cancellationToken,
                ("$category", category),
                ("$below", category is null ? null : Like.Escape(category) + "/%"),
                ("$query", query),
                ("$like", query is null ? null : "%" + Like.Escape(query) + "%")).ConfigureAwait(false);
        }
    }

    public async Task UpsertAsync(ContactRecord contact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contact);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await SqliteCommands.ExecuteAsync(
                connection,
                "INSERT INTO contact (jid, display_name, voice, updated) VALUES ($jid, $name, $voice, $updated) "
                + "ON CONFLICT (jid) DO UPDATE SET display_name = excluded.display_name, voice = excluded.voice, updated = excluded.updated",
                cancellationToken,
                null,
                ("$jid", contact.Jid),
                ("$name", contact.DisplayName),
                ("$voice", contact.Voice),
                ("$updated", SqliteCommands.ToUnixMs(contact.Updated))).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAsync(string jid, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.ExecuteAsync(
                connection, "DELETE FROM contact WHERE jid = $jid", cancellationToken, null, ("$jid", jid)).ConfigureAwait(false) > 0;
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(string jid, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection,
                "SELECT category FROM contact_category WHERE jid = $jid ORDER BY category",
                r => r.GetString(0),
                cancellationToken,
                ("$jid", jid)).ConfigureAwait(false);
        }
    }

    public async Task SetCategoriesAsync(string jid, IReadOnlyList<string> categories, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categories);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM contact_category WHERE jid = $jid", cancellationToken, transaction, ("$jid", jid)).ConfigureAwait(false);
                foreach (var category in categories.Distinct(StringComparer.Ordinal))
                {
                    await SqliteCommands.ExecuteAsync(
                        connection,
                        "INSERT INTO contact_category (jid, category) VALUES ($jid, $category)",
                        cancellationToken,
                        transaction,
                        ("$jid", jid),
                        ("$category", category)).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyList<ContactFact>> GetFactsAsync(string jid, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection, FactColumns + " WHERE jid = $jid ORDER BY field", ReadFact, cancellationToken, ("$jid", jid)).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ContactFact>> FindFactsAsync(string field, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection, FactColumns + " WHERE field = $field ORDER BY jid", ReadFact, cancellationToken, ("$field", field)).ConfigureAwait(false);
        }
    }

    public async Task UpsertFactAsync(ContactFact fact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await SqliteCommands.ExecuteAsync(
                connection,
                "INSERT INTO contact_fact (jid, field, value, source, confidence, evidence, sensitive, updated, expires) "
                + "VALUES ($jid, $field, $value, $source, $confidence, $evidence, $sensitive, $updated, $expires) "
                + "ON CONFLICT (jid, field) DO UPDATE SET value = excluded.value, source = excluded.source, "
                + "confidence = excluded.confidence, evidence = excluded.evidence, sensitive = excluded.sensitive, "
                + "updated = excluded.updated, expires = excluded.expires",
                cancellationToken,
                null,
                ("$jid", fact.Jid),
                ("$field", fact.Field),
                ("$value", fact.ValueJson),
                ("$source", SourceNames.ToName(fact.Source)),
                ("$confidence", fact.Confidence),
                ("$evidence", fact.Evidence),
                ("$sensitive", fact.Sensitive ? 1 : 0),
                ("$updated", SqliteCommands.ToUnixMs(fact.Updated)),
                ("$expires", fact.Expires is { } expires ? SqliteCommands.ToUnixMs(expires) : null)).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteFactAsync(string jid, string field, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.ExecuteAsync(
                connection,
                "DELETE FROM contact_fact WHERE jid = $jid AND field = $field",
                cancellationToken,
                null,
                ("$jid", jid),
                ("$field", field)).ConfigureAwait(false) > 0;
        }
    }

    public async Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.ExecuteAsync(
                connection,
                "DELETE FROM contact_fact WHERE expires IS NOT NULL AND expires <= $now",
                cancellationToken,
                null,
                ("$now", SqliteCommands.ToUnixMs(now))).ConfigureAwait(false);
        }
    }

    public async Task<long> AddNoteAsync(ContactNote note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(note);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var id = await SqliteCommands.ScalarAsync(
                connection,
                "INSERT INTO contact_note (jid, text, source, created) VALUES ($jid, $text, $source, $created) RETURNING id",
                cancellationToken,
                null,
                ("$jid", note.Jid),
                ("$text", note.Text),
                ("$source", SourceNames.ToName(note.Source)),
                ("$created", SqliteCommands.ToUnixMs(note.Created))).ConfigureAwait(false);
            return Convert.ToInt64(id, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public async Task<IReadOnlyList<ContactNote>> GetNotesAsync(string jid, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection,
                "SELECT id, jid, text, source, created FROM contact_note WHERE jid = $jid ORDER BY created, id",
                r => new ContactNote(r.GetInt64(0), r.GetString(1), r.GetString(2), SourceNames.FromName(r.GetString(3)), SqliteCommands.FromUnixMs(r.GetInt64(4))),
                cancellationToken,
                ("$jid", jid)).ConfigureAwait(false);
        }
    }

    private static ContactRecord ReadContact(SqliteDataReader r) => new(
        r.GetString(0),
        SqliteCommands.NullableString(r, 1),
        SqliteCommands.NullableString(r, 2),
        SqliteCommands.FromUnixMs(r.GetInt64(3)));

    private static ContactFact ReadFact(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.GetString(2),
        SourceNames.FromName(r.GetString(3)),
        r.GetDouble(4),
        SqliteCommands.NullableString(r, 5),
        r.GetInt64(6) == 1,
        SqliteCommands.FromUnixMs(r.GetInt64(7)),
        r.IsDBNull(8) ? null : SqliteCommands.FromUnixMs(r.GetInt64(8)));
}
