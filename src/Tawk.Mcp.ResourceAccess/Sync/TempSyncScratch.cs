namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>A folder under the system's temporary folder that only the user can read.</summary>
public sealed class TempSyncScratch : ISyncScratch
{
    private readonly Lock _gate = new();
    private string? _folder;

    public string NewFile()
    {
        lock (_gate)
        {
            if (_folder is null)
            {
                _folder = Path.Combine(Path.GetTempPath(), "tawk-mcp-sync-" + Guid.NewGuid().ToString("N"));
                if (OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(_folder);
                }
                else
                {
                    Directory.CreateDirectory(_folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            return Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".db");
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                if (_folder is not null && Directory.Exists(_folder))
                {
                    Directory.Delete(_folder, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A copy still held open is removed by the system's own cleanup of temporary files.
            }

            _folder = null;
        }
    }
}
