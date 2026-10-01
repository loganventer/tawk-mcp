using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Tawk.Mcp.Clients.Sessions;

/// <summary>
/// Wraps an SDK session. The SDK hands every request its own server object for the same session, so two
/// wrappers are the same session when their session ids match; a stdio connection has no id and is one session.
/// </summary>
public sealed class McpClientSession(McpServer server) : IClientSession, IEquatable<McpClientSession>
{
    public McpServer Server { get; } = server;

    public string? ClientName => Server.ClientInfo?.Name;

    public string? SessionId => Server.SessionId;

    public Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken) =>
        Server.SendNotificationAsync(method, parameters, McpJsonUtilities.DefaultOptions, cancellationToken);

    public bool Equals(McpClientSession? other) =>
        other is not null && (ReferenceEquals(Server, other.Server) || string.Equals(SessionId, other.SessionId, StringComparison.Ordinal));

    public override bool Equals(object? obj) => Equals(obj as McpClientSession);

    public override int GetHashCode() => SessionId is { } id ? StringComparer.Ordinal.GetHashCode(id) : 0;
}
