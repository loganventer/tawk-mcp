using Tawk.Mcp.Core.Media;

namespace Tawk.Mcp.ResourceAccess.Media;

/// <summary>Asks tawk where a message's file is. tawk decides which messages may be asked about.</summary>
public interface ITawkMediaSource
{
    /// <summary>
    /// Has tawk fetch the file if it has not, and waits up to <paramref name="wait"/> for it. Returns null
    /// when tawk is still downloading at the end of the wait.
    /// </summary>
    Task<MediaFile?> LocateAsync(string messageId, TimeSpan wait, CancellationToken cancellationToken);
}
