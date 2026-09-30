using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

public interface ILiveUpdatesManager
{
    TawkConnectionState ConnectionState { get; }

    /// <summary>
    /// Reads events from tawk until cancelled, subscribes to every chat after each connect,
    /// and hands each update to every sink.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken);
}
