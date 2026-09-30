using ModelContextProtocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients;

public static class WriteContexts
{
    public static WriteContext For(McpServer? server, IProgress<ProgressNotificationValue>? progress) =>
        new(ElicitationConfirmation.For(server), ApprovalProgress.Reporter(progress));
}
