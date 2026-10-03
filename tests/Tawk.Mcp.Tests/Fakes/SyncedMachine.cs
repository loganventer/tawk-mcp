using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Engines.Sync;
using Tawk.Mcp.Managers.Sync;
using Tawk.Mcp.ResourceAccess.Memory;
using Tawk.Mcp.ResourceAccess.Sync;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>One machine's memory with the real sync parts, pointed at a remote the test shares between machines.</summary>
public sealed class SyncedMachine : IDisposable
{
    public SyncedMachine(IMemoryRemote remote, string? repository = "git@example.com:someone/memory.git")
    {
        Parts = new MemoryParts();
        Snapshots = new SqliteMemorySnapshotStore(Parts.Connections, new SqliteSchemaMigrator());
        Lock = new FileSyncLock(Parts.Connections.Path + ".sync.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(Parts.Connections.Path)!);
        Sync = new MemorySyncManager(
            remote, Snapshots, new MemoryMerger(), new MemoryDigest(), new FileSyncStateStore(Parts.Connections.Path + ".sync.json"), Lock,
            new TempSyncScratch(), Parts.Contacts, new SyncOptions { Repository = repository }, Parts.Clock);
    }

    public MemoryParts Parts { get; }

    public SqliteMemorySnapshotStore Snapshots { get; }

    public FileSyncLock Lock { get; }

    public MemorySyncManager Sync { get; }

    public async Task<SyncOutcome> SyncAsync() => (await Sync.SyncAsync(CancellationToken.None)).Outcome;

    public void Dispose() => Parts.Dispose();
}
