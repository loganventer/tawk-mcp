using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class MessageTools(IMessageSendingManager sending, IMessageManagementManager messages, IAccountScope accounts)
{
    [McpServerTool(Name = "draft_message", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Put text into a chat's draft in tawk's input box so the user can edit it and send it themselves. Nothing is sent and nothing is asked. "
        + "This is the safest way to propose a message. Needs access = send in tawk. Fails if the chat already has a draft.")]
    public Task<CallToolResult> DraftMessageAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description("The proposed message text.")] string text,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => sending.DraftMessageAsync(chat, text, cancellationToken));

    [McpServerTool(Name = "send_message", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Send a WhatsApp message from the user's account." + ToolText.NeedsSend
        + " The user may edit the text while approving; the result then shows what was actually sent. Prefer draft_message when unsure.")]
    public Task<CallToolResult> SendMessageAsync(
        [Description(ToolText.Recipient)] string chat,
        [Description("The message text, up to 65536 bytes.")] string text,
        [Description("Id of a message in that chat to reply to.")] string? replyTo = null,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => sending.SendMessageAsync(chat, text, replyTo, ApprovalProgress.Reporter(progress), cancellationToken));

    [McpServerTool(Name = "react", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("React to a message with an emoji, or remove the user's reaction with an empty emoji." + ToolText.NeedsSend)]
    public Task<CallToolResult> ReactAsync(
        [Description("The message id, from read_messages.")] string messageId,
        [Description("One emoji, or an empty string to remove the reaction.")] string emoji,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => sending.ReactAsync(messageId, emoji, ApprovalProgress.Reporter(progress), cancellationToken));

    [McpServerTool(Name = "mark_read", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Mark a chat as read. Sends read receipts if the user has them on in tawk." + ToolText.NeedsSend)]
    public Task<CallToolResult> MarkReadAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => sending.MarkReadAsync(chat, ApprovalProgress.Reporter(progress), cancellationToken));

    [McpServerTool(Name = "edit_message", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Edit one of the user's own text messages, within WhatsApp's 15 minutes." + ToolText.NeedsManage)]
    public Task<CallToolResult> EditMessageAsync(
        McpServer? server,
        [Description("The message id.")] string messageId,
        [Description("The new text.")] string text,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => messages.EditMessageAsync(messageId, text, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "delete_message", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Delete a message for the user, or for everyone." + ToolText.NeedsManage + ToolText.TwoStep)]
    public Task<CallToolResult> DeleteMessageAsync(
        McpServer? server,
        [Description("The message id.")] string messageId,
        [Description("Delete it for everyone in the chat.")] bool forEveryone = false,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => messages.DeleteMessageAsync(messageId, forEveryone, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "forward_message", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Forward a message to up to 5 chats." + ToolText.NeedsManage)]
    public Task<CallToolResult> ForwardMessageAsync(
        McpServer? server,
        [Description("The message id.")] string messageId,
        [Description("Up to 5 chats (jids or names).")] string[] chats,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => messages.ForwardMessageAsync(messageId, chats, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "retry_message", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Send one of the user's failed messages again." + ToolText.NeedsManage)]
    public Task<CallToolResult> RetryMessageAsync(
        McpServer? server,
        [Description("The failed message's id.")] string messageId,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => messages.RetryMessageAsync(messageId, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "download_media", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Have tawk download a message's photo, video, audio or document in the background." + ToolText.NeedsManage)]
    public Task<CallToolResult> DownloadMediaAsync(
        McpServer? server,
        [Description("The message id.")] string messageId,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => messages.DownloadMediaAsync(messageId, WriteContexts.For(server, progress), cancellationToken));
}
