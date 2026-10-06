using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class ScheduleTools(IChatReadingManager reading, IMessageSendingManager sending, IScheduleManagementManager schedule, IAccountScope accounts)
{
    private const string When = "When to send, as tawk's /later takes it: 18:00, +30m, tomorrow 9:00, fri 17:30.";

    [McpServerTool(Name = "list_scheduled", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List messages scheduled in tawk that have not gone out yet." + ToolText.Untrusted)]
    public Task<CallToolResult> ListScheduledAsync(
        [Description("Only messages scheduled for this chat (jid or name).")] string? chat = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => reading.ListScheduledAsync(chat, cancellationToken));

    [McpServerTool(Name = "schedule_message", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Schedule a WhatsApp message to go out later from tawk. tawk-mcp moves the time by a random few seconds either way, "
        + "so it does not land on the exact minute." + ToolText.NeedsSend + " The user may edit the text while approving.")]
    public Task<CallToolResult> ScheduleMessageAsync(
        [Description(ToolText.Recipient)] string chat,
        [Description(When)] string when,
        [Description("The message text.")] string text,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => sending.ScheduleMessageAsync(chat, when, text, ApprovalProgress.Reporter(progress), cancellationToken));

    [McpServerTool(Name = "cancel_scheduled", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Cancel a scheduled message." + ToolText.NeedsManage + ToolText.TwoStep)]
    public Task<CallToolResult> CancelScheduledAsync(
        McpServer? server,
        [Description("The scheduled message's id, from list_scheduled.")] string id,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => schedule.CancelScheduledAsync(id, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "reschedule", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Move a scheduled message to another time, shifted by a random few seconds like schedule_message." + ToolText.NeedsManage)]
    public Task<CallToolResult> RescheduleAsync(
        McpServer? server,
        [Description("The scheduled message's id.")] string id,
        [Description(When)] string when,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => schedule.RescheduleAsync(id, when, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "send_scheduled_now", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Send a scheduled message now." + ToolText.NeedsManage)]
    public Task<CallToolResult> SendScheduledNowAsync(
        McpServer? server,
        [Description("The scheduled message's id.")] string id,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => schedule.SendScheduledNowAsync(id, WriteContexts.For(server, progress), cancellationToken));
}
