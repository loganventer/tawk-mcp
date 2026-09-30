using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

public interface ITawkControl
{
    TawkConnectionState State { get; }

    /// <summary>Returns the hello answer of the current connection, waiting briefly for one if needed.</summary>
    Task<HelloInfo> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends one operation and returns its result, or throws <see cref="TawkControlException"/>.
    /// <paramref name="onApprovalWaiting"/> runs when tawk reports that the request waits for the user's approval.
    /// </summary>
    Task<JsonElement> RequestAsync(
        string op,
        JsonObject? args,
        Action? onApprovalWaiting,
        CancellationToken cancellationToken);

    /// <summary>
    /// Notifications from tawk, plus <see cref="ConnectedEvent"/> and <see cref="DisconnectedEvent"/>.
    /// Every enumeration gets its own copy of the stream.
    /// </summary>
    IAsyncEnumerable<TawkEvent> Events { get; }
}
