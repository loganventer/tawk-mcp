using Microsoft.Extensions.Logging.Abstractions;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess;

[CancelAfter(10000)]
public class TawkConnectionSupervisorTests
{
    private readonly ManualTimeProvider _clock = new();

    private (TawkConnectionSupervisor Supervisor, FakeDelay Delay, CircuitBreaker Breaker, FakeSocketFileWatcher Watcher) Build(
        ITawkConnector connector, int threshold = 100)
    {
        var delay = new FakeDelay(_clock);
        var breaker = new CircuitBreaker(threshold, TimeSpan.FromSeconds(60), _clock);
        var watcher = new FakeSocketFileWatcher();
        var supervisor = new TawkConnectionSupervisor(
            connector,
            breaker,
            new ExponentialBackoffPolicy(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30), Random.Shared.NextDouble),
            delay,
            new ConnectSignal(),
            watcher,
            _clock,
            NullLogger<TawkConnectionSupervisor>.Instance);
        return (supervisor, delay, breaker, watcher);
    }

    [Test]
    public async Task Backs_off_within_bounds_until_tawk_answers()
    {
        var connector = new FakeTawkConnector(failuresBeforeSuccess: 12);
        var (supervisor, delay, _, watcher) = Build(connector);

        await supervisor.StartAsync(CancellationToken.None);
        await connector.Connected.Task;
        await supervisor.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(connector.Attempts, Is.EqualTo(13));
            Assert.That(delay.Delays, Has.Count.EqualTo(12));
            Assert.That(delay.Delays, Has.All.InRange(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30)));
            Assert.That(delay.Delays[0], Is.LessThanOrEqualTo(TimeSpan.FromMilliseconds(500)));
            Assert.That(delay.Delays[1], Is.LessThanOrEqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(watcher.WatchedPath, Is.EqualTo(connector.SocketPath));
        });
    }

    [Test]
    public async Task Opens_the_circuit_after_repeated_failures_and_waits_out_the_cooldown()
    {
        var connector = new FakeTawkConnector(failuresBeforeSuccess: 3);
        var (supervisor, delay, breaker, _) = Build(connector, threshold: 3);

        await supervisor.StartAsync(CancellationToken.None);
        await connector.Connected.Task;
        await supervisor.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(breaker.State, Is.EqualTo(CircuitState.Closed));
            Assert.That(connector.Attempts, Is.EqualTo(4));
            Assert.That(delay.Delays.Sum(d => d.TotalSeconds), Is.GreaterThanOrEqualTo(60));
        });
    }

    [Test]
    public async Task Reconnects_after_tawk_goes_away()
    {
        var connector = new FakeTawkConnector(failuresBeforeSuccess: 0);
        var (supervisor, _, _, _) = Build(connector);

        await supervisor.StartAsync(CancellationToken.None);
        await connector.Connected.Task;
        connector.Drop();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (connector.Attempts < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        await supervisor.StopAsync(CancellationToken.None);
        Assert.That(connector.Attempts, Is.EqualTo(2));
    }
}
