using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Tawk.Mcp.Clients.Sessions;

/// <summary>Wraps an SDK session. Two wrappers of the same session are equal.</summary>
public sealed class McpClientSession(McpServer server) : IClientSession, IEquatable<McpClientSession>
{
    public McpServer Server { get; } = server;

    public string? ClientName => Server.ClientInfo?.Name;

    public Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken) =>
        Server.SendNotificationAsync(method, parameters, McpJsonUtilities.DefaultOptions, cancellationToken);

    public bool Equals(McpClientSession? other) => other is not null && ReferenceEquals(Server, other.Server);

    public override bool Equals(object? obj) => Equals(obj as McpClientSession);

    public override int GetHashCode() => Server.GetHashCode();
}
