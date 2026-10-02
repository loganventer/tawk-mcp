namespace Tawk.Mcp.Core.Sync;

/// <summary>What has to change in the local database for it to hold the merge of local and remote.</summary>
public sealed record MergePlan(
    IReadOnlyList<SyncRowChange> Upserts,
    IReadOnlyList<SyncRowKey> Deletes,
    IReadOnlyList<SyncTombstone> Tombstones,
    IReadOnlyList<SyncRowKey> ClearedTombstones)
{
    public bool IsEmpty => Upserts.Count == 0 && Deletes.Count == 0 && Tombstones.Count == 0 && ClearedTombstones.Count == 0;
}
