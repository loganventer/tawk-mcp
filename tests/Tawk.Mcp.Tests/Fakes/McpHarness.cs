using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Host;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>The whole server, composed as in production, talking MCP over in-memory pipes and tawk's protocol to a fake socket.</summary>
public sealed class McpHarness : IAsyncDisposable
{
    private readonly Pipe _toServer = new();
    private readonly Pipe _toClient = new();
    private IHost? _host;

    public McpHarness()
    {
        Server = new FakeTawkServer(FakeTawkServer.TempSocketPath());
    }

    public FakeTawkServer Server { get; }

    public McpClient Client { get; private set; } = null!;

    public List<JsonRpcNotification> Notifications { get; } = [];

    public List<ElicitRequestParams> Elicitations { get; } = [];

    public string DataFile { get; } = Path.Combine(Path.GetTempPath(), "tawk-memory-" + Guid.NewGuid().ToString("N")[..8], "memory.db");

    public async Task StartAsync(
        bool elicitation = true, bool? accept = true, string clientName = "claude-code", string? protocolVersion = null, MemoryMode memory = MemoryMode.Write)
    {
        Server.Start();
        var options = new TawkMcpOptions
        {
            SocketPath = Server.SocketPath,
            BackoffInitialMs = 20,
            BackoffMaxMs = 200,
            DataFile = DataFile,
            Memory = memory,
        };
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Logging.ClearProviders();
        builder.Services.AddTawkMcp(options).WithStreamServerTransport(_toServer.Reader.AsStream(), _toClient.Writer.AsStream());
        _host = builder.Build();
        await _host.StartAsync();

        var handlers = new McpClientHandlers
        {
            NotificationHandlers =
            [
                new("notifications/claude/channel", Record),
                new(NotificationMethods.ResourceUpdatedNotification, Record),
            ],
        };
        if (elicitation)
        {
            handlers.ElicitationHandler = (request, _) =>
            {
                lock (Elicitations)
                {
                    Elicitations.Add(request!);
                }

                return ValueTask.FromResult(accept is { } yes
                    ? new ElicitResult
                    {
                        Action = "accept",
                        Content = new Dictionary<string, JsonElement> { ["confirm"] = JsonSerializer.SerializeToElement(yes) },
                    }
                    : new ElicitResult { Action = "cancel" });
            };
        }

        Client = await McpClient.CreateAsync(
            new StreamClientTransport(_toServer.Writer.AsStream(), _toClient.Reader.AsStream()),
            new McpClientOptions
            {
                ClientInfo = new Implementation { Name = clientName, Version = "1.0" },
                ProtocolVersion = protocolVersion,
                Capabilities = elicitation ? new ClientCapabilities { Elicitation = new ElicitationCapability { Form = new FormElicitationCapability() } } : new ClientCapabilities(),
                Handlers = handlers,
            });
    }

    public async Task<JsonRpcNotification> WaitForNotificationAsync(string method, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            lock (Notifications)
            {
                var found = Notifications.FirstOrDefault(n => n.Method == method);
                if (found is not null)
                {
                    return found;
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"No {method} notification arrived.");
    }

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Server.DisposeAsync();
        var folder = Path.GetDirectoryName(DataFile)!;
        if (Directory.Exists(folder))
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(folder, true);
        }
    }

    private ValueTask Record(JsonRpcNotification notification, CancellationToken cancellationToken)
    {
        lock (Notifications)
        {
            Notifications.Add(notification);
        }

        return ValueTask.CompletedTask;
    }
}
