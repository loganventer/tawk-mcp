using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>Remembers, on this machine only, what was last seen in step with the remote.</summary>
public interface ISyncStateStore
{
    SyncState Load();

    void Save(SyncState state);
}
