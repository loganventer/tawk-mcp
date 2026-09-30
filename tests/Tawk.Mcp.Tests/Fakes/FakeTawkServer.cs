using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>
/// A stand-in for tawk's control socket in a temp folder. It answers hello by itself, answers other
/// operations from a table, and can hold answers back, push notifications and stop or restart.
/// </summary>
public sealed class FakeTawkServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, string> _results = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<string> _held = [];
    private readonly List<FakeServerConnection> _connections = [];
    private readonly Lock _gate = new();
    private Socket? _listener;
    private CancellationTokenSource? _stop;
    private Task? _acceptLoop;

    public FakeTawkServer(string socketPath)
    {
        SocketPath = socketPath;
    }

    public string SocketPath { get; }

    public ConcurrentQueue<JsonObject> Received { get; } = new();

    public int HelloCount => Received.Count(r => (string?)r["op"] == "hello");

    public static string TempSocketPath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tawk-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "control.sock");
    }

    /// <summary>Answers <paramref name="op"/> with this result JSON.</summary>
    public FakeTawkServer Answer(string op, string resultJson)
    {
        _results[op] = resultJson;
        return this;
    }

    /// <summary>Does not answer <paramref name="op"/>; the test answers it later with <see cref="SendAsync"/>.</summary>
    public FakeTawkServer Hold(string op)
    {
        _held.Add(op);
        return this;
    }

    public void Start()
    {
        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }

        _stop = new CancellationTokenSource();
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        _listener.Listen(8);
        var listener = _listener;
        var token = _stop.Token;
        _acceptLoop = Task.Run(() => AcceptAsync(listener, token));
    }

    public async Task StopAsync()
    {
        if (_stop is null)
        {
            return;
        }

        await _stop.CancelAsync();
        _listener?.Dispose();
        List<FakeServerConnection> connections;
        lock (_gate)
        {
            connections = [.. _connections];
            _connections.Clear();
        }

        foreach (var connection in connections)
        {
            connection.Close();
        }

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
            }
        }

        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }

        _stop.Dispose();
        _stop = null;
    }

    /// <summary>Writes one line to every open connection.</summary>
    public async Task SendAsync(string line)
    {
        List<FakeServerConnection> connections;
        lock (_gate)
        {
            connections = [.. _connections];
        }

        foreach (var connection in connections)
        {
            await connection.WriteAsync(line);
        }
    }

    public async Task<JsonObject> WaitForAsync(string op, int count = 1, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var matches = Received.Where(r => (string?)r["op"] == op).ToList();
            if (matches.Count >= count)
            {
                return matches[count - 1];
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"The fake server never received {op} ({count}).");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        var folder = Path.GetDirectoryName(SocketPath);
        if (folder is not null && Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private async Task AcceptAsync(Socket listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var socket = await listener.AcceptAsync(token);
            var connection = new FakeServerConnection(socket);
            lock (_gate)
            {
                _connections.Add(connection);
            }

            _ = Task.Run(() => ServeAsync(connection, token), token);
        }
    }

    private async Task ServeAsync(FakeServerConnection connection, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await connection.Reader.ReadLineAsync(token);
                if (line is null)
                {
                    break;
                }

                var request = JsonNode.Parse(line)!.AsObject();
                Received.Enqueue(request);
                var id = (string)request["id"]!;
                var op = (string)request["op"]!;
                if (op == "hello")
                {
                    await connection.WriteAsync(
                        $"{{\"id\":\"{id}\",\"ok\":true,\"result\":{{\"protocol\":1,\"tawk\":\"0.6.4\",\"access\":\"send\",\"account\":{{\"jid\":\"27830000000@s.whatsapp.net\",\"name\":\"Logan\"}},\"connected\":true}}}}");
                }
                else if (!_held.Contains(op))
                {
                    var result = _results.TryGetValue(op, out var r) ? r : "{}";
                    await connection.WriteAsync($"{{\"id\":\"{id}\",\"ok\":true,\"result\":{result}}}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}
