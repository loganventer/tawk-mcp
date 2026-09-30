using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class AppTools(IAppManager app)
{
    [McpServerTool(Name = "app_status", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show tawk's version, backend, WhatsApp connection state and whether a call is ringing.")]
    public Task<CallToolResult> AppStatusAsync(CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => app.AppStatusAsync(cancellationToken));

    [McpServerTool(Name = "reconnect", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Make tawk reconnect to WhatsApp." + ToolText.NeedsManage)]
    public Task<CallToolResult> ReconnectAsync(
        McpServer? server,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => app.ReconnectAsync(WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "decline_call", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Decline the call ringing now." + ToolText.NeedsManage)]
    public Task<CallToolResult> DeclineCallAsync(
        McpServer? server,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => app.DeclineCallAsync(WriteContexts.For(server, progress), cancellationToken));
}
