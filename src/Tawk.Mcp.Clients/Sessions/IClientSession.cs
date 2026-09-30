using System.Text.Json.Nodes;

namespace Tawk.Mcp.Clients.Sessions;

/// <summary>One connected MCP client session that notifications can be sent to.</summary>
public interface IClientSession
{
    string? ClientName { get; }

    Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken);
}
