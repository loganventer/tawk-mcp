using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class StatusTools(IChatReadingManager reading, IStatusManager statuses, IAccountScope accounts)
{
    [McpServerTool(Name = "list_statuses", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List WhatsApp status updates from the user's contacts." + ToolText.Untrusted)]
    public Task<CallToolResult> ListStatusesAsync(
        [Description("Also include statuses older than a day, which tawk keeps in its archive for status_keep_days days.")] bool includeArchived = false,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => reading.ListStatusesAsync(includeArchived, cancellationToken));

    [McpServerTool(Name = "status_viewers", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List who viewed or liked one of the user's own statuses." + ToolText.Untrusted)]
    public Task<CallToolResult> StatusViewersAsync(
        [Description("The id of one of the user's statuses, from list_statuses.")] string statusId,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => statuses.StatusViewersAsync(statusId, cancellationToken));

    [McpServerTool(Name = "list_backgrounds", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the background colour names post_status accepts for text and link statuses.")]
    public Task<CallToolResult> ListBackgroundsAsync(
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => statuses.ListBackgroundsAsync(cancellationToken));

    [McpServerTool(Name = "post_status", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Post a status update as the user. Needs tawk's whatsmeow backend." + ToolText.NeedsManage)]
    public Task<CallToolResult> PostStatusAsync(
        McpServer? server,
        [Description("text, photo, video or link.")] string kind,
        [Description("The words, or the caption for a photo or video.")] string? text = null,
        [Description("Path of a photo or video on this computer.")] string? file = null,
        [Description("A colour name from list_backgrounds, for text and link statuses.")] string? background = null,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => statuses.PostStatusAsync(kind, text, file, background, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "reply_status", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Reply to a status; the reply goes to the user's chat with its author, quoting it." + ToolText.NeedsManage)]
    public Task<CallToolResult> ReplyStatusAsync(
        McpServer? server,
        [Description("The status id.")] string statusId,
        [Description("The reply text.")] string text,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => statuses.ReplyStatusAsync(statusId, text, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "like_status", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Like a status, or send a heart reply where the backend cannot like." + ToolText.NeedsManage)]
    public Task<CallToolResult> LikeStatusAsync(
        McpServer? server,
        [Description("The status id.")] string statusId,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => statuses.LikeStatusAsync(statusId, WriteContexts.For(server, progress), cancellationToken));
}
