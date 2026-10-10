using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class ChatListTools(IChatToolsManager tools, IAccountScope accounts)
{
    private const string Local = " It is kept on the user's computer and nothing is sent to WhatsApp.";

    [McpServerTool(Name = "list_labels", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the user's own labels on chats: every label in use, or the labels of one chat. Labels are the user's way of sorting "
        + "chats in tawk (/filter label NAME shows a label's chats)." + ToolText.Untrusted)]
    public Task<CallToolResult> ListLabelsAsync(
        [Description("Only this chat's labels (jid or name). Leave out for every label in use.")] string? chat = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => tools.ListLabelsAsync(chat, cancellationToken));

    [McpServerTool(Name = "set_label", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Put one of the user's labels on a chat, or take it off. A label that no chat carried before is made by using it. "
        + "Only do this when the user asks." + Local + ToolText.NeedsManage)]
    public Task<CallToolResult> SetLabelAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description("The label: up to 24 characters, no comma or slash. tawk keeps it in lower case.")] string label,
        [Description("True to put it on, false to take it off.")] bool on = true,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => tools.SetLabelAsync(chat, label, on, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "list_reminders", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the chats the user put aside with a reminder. Such a chat is out of their chat list until its time, or until "
        + "its person writes." + ToolText.Untrusted)]
    public Task<CallToolResult> ListRemindersAsync(
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => tools.ListRemindersAsync(cancellationToken));

    [McpServerTool(Name = "set_reminder", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Put a chat aside until a time, or until its person writes: it leaves the user's chat list and comes back then with a "
        + "notice. Only do this when the user asks, since it hides the chat from them." + Local + ToolText.NeedsSend)]
    public Task<CallToolResult> SetReminderAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description("When it comes back, as tawk's /remind takes it: 9:00, tomorrow, +2h, fri 17:30, or reply for no time at all (only when its person writes).")] string when,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => tools.SetReminderAsync(chat, when, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "cancel_reminder", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Bring a chat that was put aside back into the user's chat list now." + Local + ToolText.NeedsSend)]
    public Task<CallToolResult> CancelReminderAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => tools.CancelReminderAsync(chat, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "awaiting_replies", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the one-to-one chats where the user's last message has gone unanswered: the newest message is theirs and at least "
        + "the given number of days old (up to two months back). tawk works this out from the messages it keeps. Use it with due_follow_ups "
        + "when the user asks who has not answered them. Reading never marks anything as read." + ToolText.Untrusted)]
    public Task<CallToolResult> AwaitingRepliesAsync(
        [Description("How many days unanswered, 1 to 60. Leave out for the user's own setting in tawk (3 to begin with).")] int? days = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => tools.AwaitingRepliesAsync(days, cancellationToken));
}
