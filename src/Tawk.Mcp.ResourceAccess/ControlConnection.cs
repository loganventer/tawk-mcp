using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>One live connection to tawk: a single reader loop matches answers to requests by id.</summary>
internal sealed class ControlConnection : IAsyncDisposable
{
    public const string QuitMessage = "tawk quit before it answered.";

    public const string QuitWhileApprovingMessage =
        "tawk quit while this was waiting for your approval. It may or may not have gone; check the chat in tawk.";

    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly ControlLineCodec _codec;
    private readonly Action<TawkEvent> _onEvent;
    private readonly Action<ControlConnection> _onClosed;
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _readLoop;
    private int _disposed;

    private ControlConnection(
        Socket socket,
        ControlLineCodec codec,
        Action<TawkEvent> onEvent,
        Action<ControlConnection> onClosed)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
        _codec = codec;
        _onEvent = onEvent;
        _onClosed = onClosed;
        _readLoop = Task.Run(ReadLoopAsync);
    }

    public bool IsAlive => !_closed.Task.IsCompleted;

    public Task Closed => _closed.Task;

    public HelloInfo? Hello { get; set; }

    public static async Task<ControlConnection> OpenAsync(
        string socketPath,
        ControlLineCodec codec,
        Action<TawkEvent> onEvent,
        Action<ControlConnection> onClosed,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            // ENOENT, ECONNREFUSED and a stale socket file all end up here and mean tawk is not up yet.
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            socket.Dispose();
            throw TawkControlException.NotRunning(socketPath, ex);
        }

        return new ControlConnection(socket, codec, onEvent, onClosed);
    }

    /// <summary>
    /// Sends a request. With a <paramref name="timeout"/>, a request left unanswered is failed with Offline
    /// and <paramref name="onTimeout"/> runs so the caller can drop the connection.
    /// </summary>
    public async Task<JsonElement> SendAsync(
        string id,
        string op,
        JsonObject? args,
        Action? onApprovalWaiting,
        TimeSpan? timeout,
        Action? onTimeout,
        CancellationToken cancellationToken)
    {
        if (!IsAlive)
        {
            throw TawkControlException.Offline(QuitMessage);
        }

        var pending = new PendingRequest(onApprovalWaiting);
        _pending[id] = pending;
        var bytes = Encoding.UTF8.GetBytes(_codec.EncodeRequest(id, op, args) + "\n");
        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            _pending.TryRemove(id, out _);
            throw new TawkControlException(ControlErrorCode.Offline, QuitMessage, ex);
        }

        try
        {
            return timeout is { } limit
                ? await pending.Completion.Task.WaitAsync(limit, cancellationToken).ConfigureAwait(false)
                : await pending.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _pending.TryRemove(id, out _);
            onTimeout?.Invoke();
            throw TawkControlException.Offline(
                $"tawk did not answer within {timeout!.Value.TotalSeconds:0.#} s and looks hung. "
                + "tawk-mcp dropped the connection and will reconnect.");
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(id, out _);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            await _closed.Task.ConfigureAwait(false);
            return;
        }

        await _shutdown.CancelAsync().ConfigureAwait(false);
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
            // Already closed by the other side.
        }

        _socket.Dispose();
        await _readLoop.ConfigureAwait(false);
        await _stream.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
        _shutdown.Dispose();
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            using var reader = new StreamReader(_stream, new UTF8Encoding(false), false, 64 * 1024, leaveOpen: true);
            while (!_shutdown.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_shutdown.Token).ConfigureAwait(false);
                if (line is null || (line.Length > 0 && Dispatch(line)))
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The connection ended; pending requests are failed below.
        }
        finally
        {
            foreach (var id in _pending.Keys)
            {
                if (_pending.TryRemove(id, out var pending))
                {
                    pending.Completion.TrySetException(TawkControlException.Offline(
                        pending.ApprovalReported ? QuitWhileApprovingMessage : QuitMessage));
                }
            }

            _closed.TrySetResult();
            _onClosed(this);
        }
    }

    /// <summary>Handles one line and returns true when tawk said goodbye.</summary>
    private bool Dispatch(string line)
    {
        ControlFrame? frame;
        try
        {
            frame = _codec.Decode(line);
        }
        catch (JsonException)
        {
            return false;
        }

        switch (frame)
        {
            case ControlResponse response when _pending.TryRemove(response.Id, out var pending):
                if (response.Ok)
                {
                    pending.Completion.TrySetResult(response.Result);
                }
                else
                {
                    pending.Completion.TrySetException(new TawkControlException(response.Error!));
                }

                return false;
            case ControlEventFrame { Event: ApprovalEvent approval }:
                if (string.Equals(approval.State, "waiting", StringComparison.Ordinal)
                    && _pending.TryGetValue(approval.RequestId, out var waiting))
                {
                    waiting.ReportApprovalWaiting();
                }

                return false;
            case ControlEventFrame { Event: UnknownEvent }:
                return false;
            case ControlEventFrame { Event: ByeEvent bye }:
                _onEvent(bye);
                return true;
            case ControlEventFrame eventFrame:
                _onEvent(eventFrame.Event);
                return false;
            default:
                return false;
        }
    }
}
