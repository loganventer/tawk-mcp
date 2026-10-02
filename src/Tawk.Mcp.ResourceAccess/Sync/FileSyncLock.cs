namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>A lock file beside the database, held open for as long as the lock is.</summary>
public sealed class FileSyncLock(string path) : ISyncLock
{
    public IDisposable? TryAcquire()
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
