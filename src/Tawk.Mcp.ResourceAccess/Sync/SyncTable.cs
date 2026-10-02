namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>
/// How one table takes part in the memory sync: its key, its columns, where a row's source and expiry
/// are when it has them, and the parent row it cannot exist without.
/// </summary>
internal sealed record SyncTable(
    string Name,
    IReadOnlyList<string> Key,
    IReadOnlyList<string> Columns,
    string? SourceSql = null,
    string? ExpiresColumn = null,
    string? ParentTable = null,
    string? ParentKey = null,
    string? ParentColumn = null);
