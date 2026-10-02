namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>Somewhere private to keep copies of the database while a sync is under way.</summary>
public interface ISyncScratch
{
    /// <summary>A path for a new file. Nothing is created there yet.</summary>
    string NewFile();

    /// <summary>Removes every file handed out so far.</summary>
    void Clear();
}
