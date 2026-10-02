using System.Text.Json;
using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>Keeps the sync state in a small file beside the database.</summary>
public sealed class FileSyncStateStore(string path) : ISyncStateStore
{
    public SyncState Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<SyncState>(File.ReadAllText(path)) ?? new SyncState(null, null) : new SyncState(null, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Without it the next cycle looks at the remote afresh, which is always safe.
            return new SyncState(null, null);
        }
    }

    public void Save(SyncState state)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(state));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
