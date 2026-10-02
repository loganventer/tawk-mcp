namespace Tawk.Mcp.Core.Sync;

/// <summary>Everything in a memory database that syncs: the rows of each table by table name, and the deletes.</summary>
public sealed record MemorySnapshot(IReadOnlyDictionary<string, IReadOnlyList<SyncRow>> Tables, IReadOnlyList<SyncTombstone> Tombstones);
