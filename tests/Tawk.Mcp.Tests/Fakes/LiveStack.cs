using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>The real control, supervisor, breaker and watcher pointed at a socket path, with short timings.</summary>
public sealed class LiveStack : IAsyncDisposable
{
    public LiveStack(string socketPath, TimeSpan? connectWait = null, TimeSpan? requestTimeout = null, int breakerThreshold = 50, bool parkWaitingWrites = false)
    {
        Breaker = new CircuitBreaker(breakerThreshold, TimeSpan.FromSeconds(60), TimeProvider.System);
        Control = new UnixSocketTawkControl(
            new ControlSocketLocator(socketPath, _ => null, "/nonexistent"),
            new ControlLineCodec(),
            new TawkControlOptions
            {
                ConnectWait = connectWait ?? TimeSpan.FromSeconds(2),
                RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(10),
                HelloTimeout = TimeSpan.FromSeconds(2),
                ParkWaitingWrites = parkWaitingWrites,
            },
            Breaker,
            Signal,
            TimeProvider.System);
        Supervisor = new TawkConnectionSupervisor(
            Control,
            Breaker,
            new ExponentialBackoffPolicy(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(200), Random.Shared.NextDouble),
            new TimeProviderDelay(TimeProvider.System),
            Signal,
            new SocketFileWatcher(),
            TimeProvider.System,
            Logger);
    }

    public ConnectSignal Signal { get; } = new();

    public CircuitBreaker Breaker { get; }

    public UnixSocketTawkControl Control { get; }

    public TawkConnectionSupervisor Supervisor { get; }

    public CapturingLogger<TawkConnectionSupervisor> Logger { get; } = new();

    public Task StartAsync() => Supervisor.StartAsync(CancellationToken.None);

    public async Task WaitUntilConnectedAsync(int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (Control.State != TawkConnectionState.Connected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Never connected to the fake tawk.");
            }

            await Task.Delay(10);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Supervisor.StopAsync(CancellationToken.None);
        Supervisor.Dispose();
        await Control.DisposeAsync();
    }
}
