using Microsoft.Data.Sqlite;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>Opens connections to the memory database, creating and migrating it on first use.</summary>
public interface ISqliteConnectionFactory
{
    string Path { get; }

    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken);
}
