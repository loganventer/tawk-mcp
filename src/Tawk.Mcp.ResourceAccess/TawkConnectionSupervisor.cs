using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>
/// Keeps tawk-mcp connected to tawk: retries with backoff behind a circuit breaker, tries at once when the
/// socket file appears or a request is waiting, and starts over whenever tawk goes away.
/// </summary>
public sealed partial class TawkConnectionSupervisor : BackgroundService
{
    private readonly ITawkConnector _connector;
    private readonly ICircuitBreaker _breaker;
    private readonly IBackoffPolicy _backoff;
    private readonly IDelay _delay;
    private readonly ConnectSignal _signal;
    private readonly ISocketFileWatcher _watcher;
    private readonly TimeProvider _clock;
    private readonly ILogger<TawkConnectionSupervisor> _logger;

    public TawkConnectionSupervisor(
        ITawkConnector connector,
        ICircuitBreaker breaker,
        IBackoffPolicy backoff,
        IDelay delay,
        ConnectSignal signal,
        ISocketFileWatcher watcher,
        TimeProvider clock,
        ILogger<TawkConnectionSupervisor> logger)
    {
        ArgumentNullException.ThrowIfNull(connector);
        _connector = connector;
        _breaker = breaker;
        _backoff = backoff;
        _delay = delay;
        _signal = signal;
        _watcher = watcher;
        _clock = clock;
        _logger = logger;
    }

    private const int MaxWaitsKept = 100;

    /// <summary>The most recent waits chosen, for tests and diagnostics.</summary>
    public IList<TimeSpan> Waits { get; } = new List<TimeSpan>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var socketPath = _connector.SocketPath;
        _watcher.Watch(socketPath, OnSocketAppeared);
        var attempt = 0;
        var announcedWaiting = false;
        var announcedOpen = false;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_breaker.TryBeginAttempt())
                {
                    var retryAt = _breaker.RetryAt ?? _clock.GetUtcNow();
                    await WaitAsync(Max(retryAt - _clock.GetUtcNow(), TimeSpan.FromMilliseconds(50)), stoppingToken)
                        .ConfigureAwait(false);
                    continue;
                }

                HelloInfo hello;
                try
                {
                    hello = await _connector.ConnectOnceAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (TawkControlException ex)
                {
                    _breaker.RecordFailure();
                    _connector.PublishStateIfChanged();
                    if (!announcedWaiting)
                    {
                        LogWaiting(socketPath, ex.Message);
                        announcedWaiting = true;
                    }

                    if (_breaker.State == CircuitState.Open && !announcedOpen)
                    {
                        LogCircuitOpen(_breaker.ConsecutiveFailures);
                        announcedOpen = true;
                    }

                    _watcher.Refresh();
                    await WaitAsync(_backoff.NextDelay(attempt++), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                _breaker.RecordSuccess();
                attempt = 0;
                announcedWaiting = false;
                announcedOpen = false;
                LogConnected(hello.Tawk);
                await _connector.WaitForDisconnectAsync(stoppingToken).ConfigureAwait(false);
                LogWentAway();
                _watcher.Refresh();

                // A short pause first, so a tawk that accepts and drops at once cannot make this spin.
                await WaitAsync(_backoff.NextDelay(0), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    public override void Dispose()
    {
        _watcher.Dispose();
        base.Dispose();
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private void OnSocketAppeared()
    {
        _breaker.ForceTrial();
        _signal.Signal();
    }

    private async Task WaitAsync(TimeSpan wait, CancellationToken stoppingToken)
    {
        // Kept short: while tawk is not running this is called for as long as the server is.
        if (Waits.Count >= MaxWaitsKept)
        {
            Waits.RemoveAt(0);
        }

        Waits.Add(wait);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var delay = _delay.DelayAsync(wait, linked.Token);
        var signal = _signal.WaitAsync(linked.Token);
        await Task.WhenAny(delay, signal).ConfigureAwait(false);
        await linked.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(delay, signal).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // One of the two was cancelled on purpose.
        }

        stoppingToken.ThrowIfCancellationRequested();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to tawk {Version}")]
    private partial void LogConnected(string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "tawk went away; retrying")]
    private partial void LogWentAway();

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for tawk at {SocketPath}: {Reason}")]
    private partial void LogWaiting(string socketPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "tawk has not answered {Attempts} attempts; pausing retries")]
    private partial void LogCircuitOpen(int attempts);
}
