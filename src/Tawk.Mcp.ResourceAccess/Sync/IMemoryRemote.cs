namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>The copy of the memory database that every machine syncs with.</summary>
public interface IMemoryRemote
{
    /// <summary>The version of the remote file, or null when there is none yet.</summary>
    Task<string?> HeadAsync(CancellationToken cancellationToken);

    /// <summary>Writes exactly that version of the remote file to a local path.</summary>
    Task DownloadAsync(string version, string destination, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the remote file, as long as it is still at <paramref name="baseVersion"/>. Returns the new
    /// version, or null when the remote moved on in the meantime.
    /// </summary>
    Task<string?> PushAsync(string file, string? baseVersion, string message, CancellationToken cancellationToken);
}
