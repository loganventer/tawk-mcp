using Microsoft.Data.Sqlite;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>Small helpers so every store builds parameterised commands the same way.</summary>
internal static class SqliteCommands
{
    public static SqliteCommand Create(SqliteConnection connection, string sql, SqliteTransaction? transaction, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    public static async Task<int> ExecuteAsync(
        SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null, params (string Name, object? Value)[] parameters)
    {
        var command = Create(connection, sql, transaction, parameters);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<object?> ScalarAsync(
        SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null, params (string Name, object? Value)[] parameters)
    {
        var command = Create(connection, sql, transaction, parameters);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<IReadOnlyList<T>> QueryAsync<T>(
        SqliteConnection connection, string sql, Func<SqliteDataReader, T> map, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        var command = Create(connection, sql, null, parameters);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                var rows = new List<T>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(map(reader));
                }

                return rows;
            }
        }
    }

    public static string? NullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static long ToUnixMs(DateTimeOffset time) => time.ToUnixTimeMilliseconds();

    public static DateTimeOffset FromUnixMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms);
}
