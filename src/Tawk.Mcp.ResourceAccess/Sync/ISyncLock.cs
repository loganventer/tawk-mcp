namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>Lets one tawk-mcp at a time sync a memory database on this machine.</summary>
public interface ISyncLock
{
    /// <summary>The lock, released when disposed, or null while another holds it.</summary>
    IDisposable? TryAcquire();
}
