namespace Tawk.Mcp.Core.Sync;

/// <summary>
/// What this machine saw when it and the remote last held the same content: the remote file's version
/// and the digest of that content. Kept beside the database, never in it.
/// </summary>
public sealed record SyncState(string? RemoteVersion, string? Digest);
