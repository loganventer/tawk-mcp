using System.Globalization;
using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.ResourceAccess.Sync;

public sealed class SqliteMemorySnapshotStore(ISqliteConnectionFactory connections, ISchemaMigrator migrator) : IMemorySnapshotStore
{
    // Two machines can each have made a voice the default; the one set last stays.
    private const string OneDefaultVoice =
        "UPDATE voice SET is_default = 0 WHERE is_default = 1 AND name <> "
        + "(SELECT name FROM voice WHERE is_default = 1 ORDER BY updated DESC, name DESC LIMIT 1)";

    public async Task<MemorySnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await ReadAsync(connection, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<MemorySnapshot> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var raw = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await using (raw.ConfigureAwait(false))
            {
                await raw.OpenAsync(cancellationToken).ConfigureAwait(false);
                var version = Convert.ToInt32(
                    await SqliteCommands.ScalarAsync(raw, "PRAGMA user_version;", cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
                if (version > SqliteSchemaMigrator.CurrentVersion)
                {
                    throw new MemoryException("The remote memory was written by a newer tawk-mcp. Update tawk-mcp on this machine to sync again.");
                }
            }

            using var copy = new SqliteConnectionFactory(path, migrator);
            var connection = await copy.OpenAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ReadAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);

                // Lets go of the copy, so it can be deleted on every system.
                SqliteConnection.ClearPool(connection);
            }
        }
        catch (SqliteException ex)
        {
            throw new MemoryException("The remote memory is not a database tawk-mcp can read: " + ex.Message, ex);
        }
    }

    public async Task ApplyAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var tables = SyncTables.All.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = connection.BeginTransaction();
            await using (transaction.ConfigureAwait(false))
            {
                foreach (var delete in plan.Deletes)
                {
                    if (tables.TryGetValue(delete.Table, out var table))
                    {
                        await DeleteAsync(connection, transaction, table, delete.Key, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Parents go in before the rows that need them; a row whose parent is gone is left out.
                foreach (var table in SyncTables.All)
                {
                    var sql = UpsertSql(table);
                    foreach (var change in plan.Upserts.Where(u => u.Table == table.Name))
                    {
                        var parameters = change.Row.Values.Select((value, i) => ("$c" + i.ToString(CultureInfo.InvariantCulture), value)).ToArray();
                        await SqliteCommands.ExecuteAsync(connection, sql, cancellationToken, transaction, parameters).ConfigureAwait(false);
                    }
                }

                await SqliteCommands.ExecuteAsync(connection, OneDefaultVoice, cancellationToken, transaction).ConfigureAwait(false);
                foreach (var tombstone in plan.Tombstones)
                {
                    await Tombstones.MarkAsync(
                        connection, transaction, tombstone.Kind, tombstone.Key, DateTimeOffset.FromUnixTimeMilliseconds(tombstone.Deleted),
                        cancellationToken).ConfigureAwait(false);
                }

                foreach (var clear in plan.ClearedTombstones)
                {
                    await Tombstones.ClearAsync(connection, transaction, clear.Table, clear.Key, cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task BackupAsync(string destination, CancellationToken cancellationToken)
    {
        var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await SqliteCommands.ExecuteAsync(connection, "VACUUM INTO $path", cancellationToken, null, ("$path", destination)).ConfigureAwait(false);
        }
    }

    private static async Task<MemorySnapshot> ReadAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var tables = new Dictionary<string, IReadOnlyList<SyncRow>>(StringComparer.Ordinal);
        foreach (var table in SyncTables.All)
        {
            var keys = table.Key.Select(k => IndexOf(table, k)).ToArray();
            var updated = IndexOf(table, "updated");
            var expires = table.ExpiresColumn is null ? -1 : IndexOf(table, table.ExpiresColumn);
            var count = table.Columns.Count;
            tables[table.Name] = await SqliteCommands.QueryAsync(
                connection,
                $"SELECT {string.Join(", ", table.Columns)}, {table.SourceSql ?? "NULL"} FROM {table.Name}",
                r =>
                {
                    var values = new object?[count];
                    for (var i = 0; i < count; i++)
                    {
                        values[i] = r.IsDBNull(i) ? null : r.GetValue(i);
                    }

                    return new SyncRow(
                        Tombstones.Key(keys.Select(k => Convert.ToString(values[k], CultureInfo.InvariantCulture) ?? string.Empty).ToArray()),
                        r.IsDBNull(count) ? null : SourceNames.FromName(r.GetString(count)),
                        Convert.ToInt64(values[updated], CultureInfo.InvariantCulture),
                        expires < 0 || values[expires] is null ? null : Convert.ToInt64(values[expires], CultureInfo.InvariantCulture),
                        values);
                },
                cancellationToken).ConfigureAwait(false);
        }

        var gone = await SqliteCommands.QueryAsync(
            connection,
            "SELECT kind, key, deleted FROM sync_tombstone",
            r => new SyncTombstone(r.GetString(0), r.GetString(1), r.GetInt64(2)),
            cancellationToken).ConfigureAwait(false);
        return new MemorySnapshot(tables, gone);
    }

    private static int IndexOf(SyncTable table, string column)
    {
        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (table.Columns[i] == column)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"{table.Name} has no column {column}.");
    }

    private static Task<int> DeleteAsync(
        SqliteConnection connection, SqliteTransaction transaction, SyncTable table, string key, CancellationToken cancellationToken)
    {
        var parts = key.Split('\n', table.Key.Count);
        var where = string.Join(" AND ", table.Key.Select((column, i) => $"{column} = $k{i.ToString(CultureInfo.InvariantCulture)}"));
        var parameters = parts.Select((part, i) => ("$k" + i.ToString(CultureInfo.InvariantCulture), (object?)part)).ToArray();
        return parts.Length == table.Key.Count
            ? SqliteCommands.ExecuteAsync(connection, $"DELETE FROM {table.Name} WHERE {where}", cancellationToken, transaction, parameters)
            : Task.FromResult(0);
    }

    private static string UpsertSql(SyncTable table)
    {
        var values = string.Join(", ", table.Columns.Select((_, i) => "$c" + i.ToString(CultureInfo.InvariantCulture)));
        var guard = table.ParentTable is null
            ? "true"
            : $"EXISTS (SELECT 1 FROM {table.ParentTable} WHERE {table.ParentKey} = $c{IndexOf(table, table.ParentColumn!).ToString(CultureInfo.InvariantCulture)})";
        var set = string.Join(", ", table.Columns.Where(c => !table.Key.Contains(c)).Select(c => $"{c} = excluded.{c}"));
        return $"INSERT INTO {table.Name} ({string.Join(", ", table.Columns)}) SELECT {values} WHERE {guard} "
            + $"ON CONFLICT ({string.Join(", ", table.Key)}) DO UPDATE SET {set}";
    }
}
