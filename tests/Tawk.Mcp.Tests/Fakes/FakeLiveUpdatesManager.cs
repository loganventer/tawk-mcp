using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class FakeLiveUpdatesManager : ILiveUpdatesManager
{
    public TawkConnectionState ConnectionState { get; set; } = TawkConnectionState.Connected;

    public Task RunAsync(CancellationToken cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
}
