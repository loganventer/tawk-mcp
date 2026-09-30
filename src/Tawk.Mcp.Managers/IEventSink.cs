using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

/// <summary>Somewhere live updates go: MCP resource notifications, a channel, an event stream.</summary>
public interface IEventSink
{
    Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken);
}
