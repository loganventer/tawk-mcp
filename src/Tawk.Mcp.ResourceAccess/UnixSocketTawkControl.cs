using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>
/// Talks to tawk over its Unix control socket. <see cref="TawkConnectionSupervisor"/> owns connecting;
/// requests use the current connection, wait briefly for one in progress, and never hang when tawk is down.
/// </summary>
public sealed class UnixSocketTawkControl : ITawkControl, ITawkConnector, IAsyncDisposable
{
    // Writes may wait for the user to approve them in tawk, so they have no answer timeout.
    private static readonly HashSet<string> WriteOps = new(StringComparer.Ordinal)
    {
        "send_message", "react", "schedule_message", "mark_read", "confirm",
        "edit_message", "delete_message", "forward_message", "retry_message", "download_media",
        "set_chat", "set_chat_theme", "clear_chat", "delete_chat", "export_chat", "block", "unblock",
        "cancel_scheduled", "reschedule", "send_scheduled_now",
        "post_status", "reply_status", "like_status",
        "set_profile", "set_profile_photo", "remove_profile_photo", "set_setting", "reconnect", "decline_call",
    };

    private readonly IControlSocketLocator _locator;
    private readonly ControlLineCodec _codec;
    private readonly TawkControlOptions _options;
    private readonly ICircuitBreaker _breaker;
    private readonly ConnectSignal _signal;
    private readonly TimeProvider _clock;
    private readonly EventBroadcaster _events = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly Lock _stateGate = new();
    private ControlConnection? _connection;
    private TaskCompletionSource<ControlConnection> _connected = NewConnectedSource();
    private TawkConnectionState _publishedState = TawkConnectionState.Waiting;
    private long _nextId;

    public UnixSocketTawkControl(
        IControlSocketLocator locator,
        ControlLineCodec codec,
        TawkControlOptions options,
        ICircuitBreaker breaker,
        ConnectSignal signal,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(breaker);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(clock);
        _locator = locator;
        _codec = codec;
        _options = options;
        _breaker = breaker;
        _signal = signal;
        _clock = clock;
    }

    public string SocketPath => _locator.Locate();

    public TawkConnectionState State => ComputeState();

    public IAsyncEnumerable<TawkEvent> Events => _events.SubscribeAsync();

    public async Task<HelloInfo> ConnectAsync(CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return connection.Hello!;
    }

    public async Task<JsonElement> RequestAsync(
        string op,
        JsonObject? args,
        Action? onApprovalWaiting,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(op);
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        TimeSpan? timeout = WriteOps.Contains(op) ? null : _options.RequestTimeout;
        return await connection.SendAsync(
            NextId(), op, args, onApprovalWaiting, timeout, () => DropHung(connection), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<HelloInfo> ConnectOnceAsync(CancellationToken cancellationToken)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsAlive: true } current)
            {
                return current.Hello!;
            }

            var connection = await ControlConnection.OpenAsync(
                SocketPath, _codec, _events.Publish, OnConnectionClosed, cancellationToken).ConfigureAwait(false);
            try
            {
                connection.Hello = await HelloAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            if (!connection.IsAlive)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw TawkControlException.NotRunning(SocketPath);
            }

            _connection = connection;
            _connected.TrySetResult(connection);
            PublishStateIfChanged(connection.Hello);
            return connection.Hello;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public Task WaitForDisconnectAsync(CancellationToken cancellationToken) =>
        _connection is { } connection ? connection.Closed.WaitAsync(cancellationToken) : Task.CompletedTask;

    /// <summary>Publishes a state event when the breaker has moved, so watchers see circuit_open.</summary>
    public void PublishStateIfChanged(HelloInfo? hello = null)
    {
        lock (_stateGate)
        {
            var state = ComputeState();
            if (state == _publishedState && hello is null)
            {
                return;
            }

            _publishedState = state;
            _events.Publish(new ConnectionStateEvent(state, hello));
        }
    }

    public async ValueTask DisposeAsync()
    {
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        _connectLock.Dispose();
    }

    private static TaskCompletionSource<ControlConnection> NewConnectedSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TawkConnectionState ComputeState()
    {
        if (_connection is { IsAlive: true })
        {
            return TawkConnectionState.Connected;
        }

        return _breaker.State == CircuitState.Open ? TawkConnectionState.CircuitOpen : TawkConnectionState.Waiting;
    }

    private async Task<ControlConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsAlive: true } current)
        {
            return current;
        }

        ThrowIfCircuitOpen();
        _signal.Signal();
        var waiting = _connected.Task;
        try
        {
            var connection = await waiting.WaitAsync(_options.ConnectWait, _clock, cancellationToken).ConfigureAwait(false);
            if (connection.IsAlive)
            {
                return connection;
            }
        }
        catch (TimeoutException)
        {
            // Fall through to the not running error below.
        }

        ThrowIfCircuitOpen();
        throw TawkControlException.NotRunning(SocketPath);
    }

    private void ThrowIfCircuitOpen()
    {
        if (_breaker.State != CircuitState.Open)
        {
            return;
        }

        var retryAt = _breaker.RetryAt ?? _clock.GetUtcNow();
        var seconds = Math.Max(0, Math.Ceiling((retryAt - _clock.GetUtcNow()).TotalSeconds));
        throw new TawkControlException(
            ControlErrorCode.NotRunning,
            string.Create(
                CultureInfo.InvariantCulture,
                $"tawk has not answered {_breaker.ConsecutiveFailures} attempts; next try in {seconds}s (or as soon as its control socket appears). Socket: {SocketPath}"));
    }

    private async Task<HelloInfo> HelloAsync(ControlConnection connection, CancellationToken cancellationToken)
    {
        var args = new JsonObject
        {
            ["client"] = _options.Client,
            ["version"] = _options.Version,
            ["protocol"] = _options.Protocol,
            ["origin"] = _options.Origin,
        };
        try
        {
            var result = await connection.SendAsync(
                NextId(), "hello", args, null, _options.HelloTimeout, null, cancellationToken).ConfigureAwait(false);
            return ControlLineCodec.Deserialize<HelloInfo>(result);
        }
        catch (TawkControlException ex) when (ex.Code == ControlErrorCode.Offline)
        {
            throw TawkControlException.NotRunning(SocketPath, ex);
        }
    }

    private void DropHung(ControlConnection connection)
    {
        _breaker.RecordFailure();
        _ = connection.DisposeAsync().AsTask();
    }

    private void OnConnectionClosed(ControlConnection connection)
    {
        if (Interlocked.CompareExchange(ref _connection, null, connection) == connection)
        {
            Interlocked.Exchange(ref _connected, NewConnectedSource());
        }

        PublishStateIfChanged();

        // The connection is finished with: let go of its socket now instead of leaving it to the finaliser.
        _ = connection.DisposeAsync().AsTask();
    }

    private string NextId() => Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);
}
