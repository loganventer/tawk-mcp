using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>The connection lifecycle, driven by <see cref="TawkConnectionSupervisor"/>.</summary>
public interface ITawkConnector
{
    string SocketPath { get; }

    /// <summary>Opens the socket, says hello and re-sends the current subscription. Throws when tawk is not up.</summary>
    Task<HelloInfo> ConnectOnceAsync(CancellationToken cancellationToken);

    /// <summary>Completes when the current connection ends, or at once when there is none.</summary>
    Task WaitForDisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Publishes a state event when the state has moved, for example when the circuit opens.</summary>
    void PublishStateIfChanged(HelloInfo? hello = null);
}
