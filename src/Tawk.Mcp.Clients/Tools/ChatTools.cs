using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class ChatTools(IChatReadingManager reading, IChatManagementManager chats, IAccountScope accounts)
{
    [McpServerTool(Name = "list_chats", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the user's WhatsApp chats in tawk, newest first with pinned chats on top. Locked and hidden chats are never shown." + ToolText.Untrusted)]
    public Task<CallToolResult> ListChatsAsync(
        [Description("Only chats whose name contains this text.")] string? filter = null,
        [Description("Only chats with unread messages.")] bool unreadOnly = false,
        [Description("How many chats, 1 to 500. Default 50.")] int? limit = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => reading.ListChatsAsync(filter, unreadOnly, limit, cancellationToken));

    [McpServerTool(Name = "read_messages", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Read messages from one chat, oldest first. Does not mark anything as read. Each line ends with the message id." + ToolText.Untrusted)]
    public Task<CallToolResult> ReadMessagesAsync(
        [Description("The chat's jid, or its name (case-insensitive, whole name or start of a word).")] string chat,
        [Description("Unix seconds; only messages older than this. Use the value from an earlier call to page back.")] long? before = null,
        [Description("How many messages, 1 to 200. Default 30.")] int? limit = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => reading.ReadMessagesAsync(chat, before, limit, cancellationToken));

    [McpServerTool(Name = "search_messages", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Search message text across chats, or in one chat, newest first." + ToolText.Untrusted)]
    public Task<CallToolResult> SearchMessagesAsync(
        [Description("Text to look for.")] string query,
        [Description("Limit the search to this chat (jid or name).")] string? chat = null,
        [Description("How many results, 1 to 100. Default 20.")] int? limit = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => reading.SearchMessagesAsync(query, chat, limit, cancellationToken));

    [McpServerTool(Name = "unread_summary", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Count unread messages and list the chats that have them. Does not mark anything as read." + ToolText.Untrusted)]
    public Task<CallToolResult> UnreadSummaryAsync(
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => reading.UnreadSummaryAsync(cancellationToken));

    [McpServerTool(Name = "get_chat_info", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show a chat's details: about text, and members and admins for groups." + ToolText.Untrusted)]
    public Task<CallToolResult> GetChatInfoAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => reading.ChatInfoAsync(chat, cancellationToken));

    [McpServerTool(Name = "set_chat", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Mute, pin, archive or lock a chat. Locking hides the chat behind tawk's soft lock, which also hides it from tawk-mcp." + ToolText.NeedsManage)]
    public Task<CallToolResult> SetChatAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description("false to unmute, true to mute always, or a number of seconds.")] string? muted = null,
        [Description("Pin or unpin.")] bool? pinned = null,
        [Description("Archive or unarchive.")] bool? archived = null,
        [Description("Only true: lock the chat.")] bool? locked = null,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => chats.SetChatAsync(chat, muted, pinned, archived, locked, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "set_chat_theme", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Give a chat its own tawk theme, from list_themes, or an empty theme for the app theme." + ToolText.NeedsManage)]
    public Task<CallToolResult> SetChatThemeAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description("A theme id, or an empty string.")] string theme,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => chats.SetChatThemeAsync(chat, theme, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "clear_chat", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Remove a chat's messages from this computer." + ToolText.NeedsManage + ToolText.TwoStep)]
    public Task<CallToolResult> ClearChatAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => chats.ClearChatAsync(chat, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "delete_chat", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Delete a chat here and on the user's phone." + ToolText.NeedsManage + ToolText.TwoStep)]
    public Task<CallToolResult> DeleteChatAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => chats.DeleteChatAsync(chat, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "export_chat", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Export a chat to a folder in the user's downloads folder." + ToolText.NeedsManage)]
    public Task<CallToolResult> ExportChatAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description("Include media files.")] bool withMedia = false,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => chats.ExportChatAsync(chat, withMedia, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "block", Destructive = true, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Block a person." + ToolText.NeedsManage + ToolText.TwoStep)]
    public Task<CallToolResult> BlockAsync(
        McpServer? server,
        [Description("The person's chat jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => chats.BlockAsync(chat, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "unblock", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Unblock a person." + ToolText.NeedsManage)]
    public Task<CallToolResult> UnblockAsync(
        McpServer? server,
        [Description("The person's chat jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => chats.UnblockAsync(chat, WriteContexts.For(server, progress), cancellationToken));
}
