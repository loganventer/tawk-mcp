using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

public interface IAppManager
{
    Task<string> AppStatusAsync(CancellationToken cancellationToken);

    Task<string> ReconnectAsync(WriteContext context, CancellationToken cancellationToken);

    Task<string> DeclineCallAsync(WriteContext context, CancellationToken cancellationToken);
}
