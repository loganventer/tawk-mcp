using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>
/// Contact profiles. The contact and its fields are rows of their own; each contact also has an OKF concept,
/// and its notes are observation concepts linked to it, so they sit with the rest of the knowledge.
/// </summary>
public sealed class SqliteContactStore(ISqliteConnectionFactory connections, OkfProducer producer, TimeProvider clock) : IContactStore
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
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                await SqliteCommands.ExecuteAsync(
                    connection,
                    "INSERT INTO contact (jid, display_name, voice, updated) VALUES ($jid, $name, $voice, $updated) "
                    + "ON CONFLICT (jid) DO UPDATE SET display_name = excluded.display_name, voice = excluded.voice, updated = excluded.updated",
                    cancellationToken,
                    transaction,
                    ("$jid", contact.Jid),
                    ("$name", contact.DisplayName),
                    ("$voice", contact.Voice),
                    ("$updated", SqliteCommands.ToUnixMs(contact.Updated))).ConfigureAwait(false);

                // The contact's concept: made with it, and only its title follows a later change.
                await SqliteCommands.ExecuteAsync(
                    connection,
                    "INSERT INTO okf_concept (id, type, title, description, resource, tags, generated_by, generated_at, verified, "
                    + "status, stale_after, sources, extra, body, updated) VALUES ($id, $type, $name, NULL, $resource, '[]', $by, "
                    + "$updated, '[]', 'stable', NULL, '[]', '{}', '', $updated) "
                    + "ON CONFLICT (id) DO UPDATE SET title = excluded.title, updated = excluded.updated",
                    cancellationToken,
                    transaction,
                    ("$id", OkfIds.Contact(contact.Jid)),
                    ("$type", OkfTypes.Contact),
                    ("$name", contact.DisplayName),
                    ("$resource", "whatsapp:" + contact.Jid),
                    ("$by", producer.Actor),
                    ("$updated", SqliteCommands.ToUnixMs(contact.Updated))).ConfigureAwait(false);
                await Tombstones.ClearAsync(connection, transaction, Tombstones.Contact, contact.Jid, cancellationToken).ConfigureAwait(false);
                await Tombstones.ClearAsync(connection, transaction, Tombstones.Concept, OkfIds.Contact(contact.Jid), cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Removes the contact with its fields, its concept and every observation about it.</summary>
    public async Task<bool> DeleteAsync(string jid, CancellationToken cancellationToken)
    {
        var concept = OkfIds.Contact(jid);
        var now = clock.GetUtcNow();
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var about = await SqliteCommands.QueryAsync(
                connection,
                "SELECT c.id FROM okf_concept c WHERE c.type = $type AND EXISTS "
                + "(SELECT 1 FROM okf_link l WHERE l.from_id = c.id AND l.to_id = $to AND l.label = $about)",
                r => r.GetString(0),
                cancellationToken,
                ("$type", OkfTypes.Observation),
                ("$to", concept),
                ("$about", OkfIds.About)).ConfigureAwait(false);
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                foreach (var id in about.Append(concept))
                {
                    var gone = await SqliteCommands.ExecuteAsync(
                        connection, "DELETE FROM okf_concept WHERE id = $id", cancellationToken, transaction, ("$id", id)).ConfigureAwait(false) > 0;
                    if (gone)
                    {
                        await Tombstones.MarkAsync(connection, transaction, Tombstones.Concept, id, now, cancellationToken).ConfigureAwait(false);
                    }
                }

                var removed = await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM contact WHERE jid = $jid", cancellationToken, transaction, ("$jid", jid)).ConfigureAwait(false) > 0;
                if (removed)
                {
                    await Tombstones.MarkAsync(connection, transaction, Tombstones.Contact, jid, now, cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return removed;
            }
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
        var before = await GetCategoriesAsync(jid, cancellationToken).ConfigureAwait(false);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                var now = clock.GetUtcNow();
                var wanted = categories.Distinct(StringComparer.Ordinal).ToList();
                foreach (var gone in before.Except(wanted, StringComparer.Ordinal))
                {
                    await Tombstones.MarkAsync(
                        connection, transaction, Tombstones.ContactCategory, Tombstones.Key(jid, gone), now, cancellationToken).ConfigureAwait(false);
                }

                await SqliteCommands.ExecuteAsync(
                    connection, "DELETE FROM contact_category WHERE jid = $jid", cancellationToken, transaction, ("$jid", jid)).ConfigureAwait(false);
                foreach (var category in wanted)
                {
                    await SqliteCommands.ExecuteAsync(
                        connection,
                        "INSERT INTO contact_category (jid, category, updated) VALUES ($jid, $category, $updated)",
                        cancellationToken,
                        transaction,
                        ("$jid", jid),
                        ("$category", category),
                        ("$updated", SqliteCommands.ToUnixMs(now))).ConfigureAwait(false);
                    await Tombstones.ClearAsync(
                        connection, transaction, Tombstones.ContactCategory, Tombstones.Key(jid, category), cancellationToken).ConfigureAwait(false);
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
            await Tombstones.ClearAsync(connection, null, Tombstones.ContactFact, Tombstones.Key(fact.Jid, fact.Field), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteFactAsync(string jid, string field, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var removed = await SqliteCommands.ExecuteAsync(
                connection,
                "DELETE FROM contact_fact WHERE jid = $jid AND field = $field",
                cancellationToken,
                null,
                ("$jid", jid),
                ("$field", field)).ConfigureAwait(false) > 0;
            if (removed)
            {
                await Tombstones.MarkAsync(connection, null, Tombstones.ContactFact, Tombstones.Key(jid, field), clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            }

            return removed;
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

    /// <summary>A note is an observation concept linked to the contact's concept. Returns its row number.</summary>
    public async Task<long> AddNoteAsync(ContactNote note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(note);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                // The id comes from the contact and the time; two notes in the same millisecond move the later one on.
                var created = note.Created;
                var id = OkfIds.Observation(note.Jid, created);
                while (await ExistsAsync(connection, transaction, id, cancellationToken).ConfigureAwait(false))
                {
                    created = created.AddMilliseconds(1);
                    id = OkfIds.Observation(note.Jid, created);
                }

                var by = OkfActors.For(note.Source, note.Jid, producer.Version);
                IReadOnlyList<OkfVerification> verified = note.Source == FactSource.User ? [new OkfVerification(OkfActors.Self, created)] : [];
                await OkfRows.UpsertConceptAsync(
                    connection,
                    transaction,
                    new OkfConcept(
                        id, OkfTypes.Observation, null, null, null, ["general"], by, created, verified, OkfStatus.Stable, null, [],
                        StoreJson.Write(new Dictionary<string, string> { ["source"] = SourceNames.ToName(note.Source) }), note.Text, created),
                    cancellationToken).ConfigureAwait(false);
                await OkfRows.UpsertLinkAsync(
                    connection,
                    transaction,
                    new OkfLink(id, OkfIds.Contact(note.Jid), OkfIds.About, null, note.Source, 1.0, created),
                    cancellationToken).ConfigureAwait(false);
                var row = await SqliteCommands.ScalarAsync(
                    connection, "SELECT rowid FROM okf_concept WHERE id = $id", cancellationToken, transaction, ("$id", id)).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Convert.ToInt64(row, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>The observations about a contact, oldest first. Sensitive ones are left to the knowledge tools.</summary>
    public async Task<IReadOnlyList<ContactNote>> GetNotesAsync(string jid, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await SqliteCommands.QueryAsync(
                connection,
                "SELECT c.rowid, c.body, COALESCE(json_extract(c.extra, '$.source'), 'inferred'), c.generated_at FROM okf_concept c "
                + "WHERE c.type = $type AND COALESCE(json_extract(c.extra, '$.sensitive'), 0) = 0 AND EXISTS (SELECT 1 FROM okf_link l WHERE l.from_id = c.id AND l.to_id = $to AND l.label = $about) "
                + "ORDER BY c.generated_at, c.rowid",
                r => new ContactNote(r.GetInt64(0), jid, r.GetString(1), SourceNames.FromName(r.GetString(2)), SqliteCommands.FromUnixMs(r.GetInt64(3))),
                cancellationToken,
                ("$type", OkfTypes.Observation),
                ("$to", OkfIds.Contact(jid)),
                ("$about", OkfIds.About)).ConfigureAwait(false);
        }
    }

    private static async Task<bool> ExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string id, CancellationToken cancellationToken) =>
        await SqliteCommands.ScalarAsync(
            connection, "SELECT 1 FROM okf_concept WHERE id = $id", cancellationToken, transaction, ("$id", id)).ConfigureAwait(false) is not null;

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
