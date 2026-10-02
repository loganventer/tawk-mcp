using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Core.Sync;

/// <summary>
/// One row of a synced table, as the merge sees it: its key, who it came from when the table records
/// that, when it last changed, when it lapses, and every column's value in the table's column order.
/// </summary>
public sealed record SyncRow(string Key, FactSource? Source, long Updated, long? Expires, IReadOnlyList<object?> Values);
