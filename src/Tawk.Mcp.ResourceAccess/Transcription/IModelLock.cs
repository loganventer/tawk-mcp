namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// One loaded model for the whole machine. Several tawk-mcp processes may run at once, one for each agent
/// session, and only the one holding this may have a model in memory.
/// </summary>
public interface IModelLock
{
    /// <summary>Waits up to <paramref name="wait"/> for the lock. Returns null when another process still holds it.</summary>
    Task<IDisposable?> AcquireAsync(TimeSpan wait, CancellationToken cancellationToken);
}
