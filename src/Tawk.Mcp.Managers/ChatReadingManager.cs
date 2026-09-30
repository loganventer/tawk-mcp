using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class ChatReadingManager : IChatReadingManager
{
    private const int DraftContextMessages = 30;

    private readonly ITawkControl _control;
    private readonly ITranscriptFormatter _transcript;
    private readonly IChatDirectoryFormatter _directory;
    private readonly IUntrustedTextFence _fence;
    private readonly ICatchUpPlanner _catchUp;
    private readonly IDraftReplyPlanner _draft;

    public ChatReadingManager(
        ITawkControl control,
        ITranscriptFormatter transcript,
        IChatDirectoryFormatter directory,
        IUntrustedTextFence fence,
        ICatchUpPlanner catchUp,
        IDraftReplyPlanner draft)
    {
        _control = control;
        _transcript = transcript;
        _directory = directory;
        _fence = fence;
        _catchUp = catchUp;
        _draft = draft;
    }

    public async Task<string> ListChatsAsync(string? filter, bool unreadOnly, int? limit, CancellationToken cancellationToken)
    {
        var args = new JsonObject();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            args["filter"] = filter;
        }

        if (unreadOnly)
        {
            args["unread_only"] = true;
        }

        if (Limits.Clamp(limit, 500) is { } l)
        {
            args["limit"] = l;
        }

        var list = await _control.RequestAsync<ChatList>("list_chats", args, cancellationToken).ConfigureAwait(false);
        var header = Count(list.Chats.Count, "chat") + ", newest first with pinned chats on top. Each line: name <jid> (flags) last time: preview.";
        return header + "\n" + _fence.Wrap("the chat list from tawk", _directory.FormatChats(list.Chats));
    }

    public async Task<string> ReadMessagesAsync(string chat, long? before, int? limit, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["chat"] = chat };
        if (before is > 0)
        {
            args["before"] = before.Value;
        }

        if (Limits.Clamp(limit, 200) is { } l)
        {
            args["limit"] = l;
        }

        var page = await _control.RequestAsync<MessagePage>("read_messages", args, cancellationToken).ConfigureAwait(false);
        var text = new StringBuilder();
        text.Append(Count(page.Messages.Count, "message")).Append(", oldest first. Chat jid: ").Append(page.Chat.Jid).Append('\n');
        text.Append(_fence.Wrap(
            "messages from a WhatsApp chat",
            _directory.FormatChat(page.Chat) + "\n\n" + _transcript.FormatMessages(page.Messages)));
        text.Append('\n').Append(page.NextBefore > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Older messages exist: call read_messages again with before={page.NextBefore}.")
            : "There are no older messages stored.");
        return text.ToString();
    }

    public async Task<string> SearchMessagesAsync(string query, string? chat, int? limit, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["query"] = query };
        if (!string.IsNullOrWhiteSpace(chat))
        {
            args["chat"] = chat;
        }

        if (Limits.Clamp(limit, 100) is { } l)
        {
            args["limit"] = l;
        }

        var found = await _control.RequestAsync<SearchResult>("search_messages", args, cancellationToken).ConfigureAwait(false);
        return Count(found.Messages.Count, "match") + ", newest first.\n"
            + _fence.Wrap("search results from WhatsApp chats", _transcript.FormatMessages(found.Messages, includeChat: true));
    }

    public async Task<string> UnreadSummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await _control.RequestAsync<UnreadSummary>("unread_summary", null, cancellationToken).ConfigureAwait(false);
        return string.Create(
                CultureInfo.InvariantCulture,
                $"{summary.Total} unread messages, {summary.Mentions} mentioning you, in {summary.Chats.Count} chats. Reading does not mark anything as read.\n")
            + _fence.Wrap("the chats with unread messages", _directory.FormatChats(summary.Chats));
    }

    public async Task<string> ChatInfoAsync(string chat, CancellationToken cancellationToken)
    {
        var info = await _control.RequestAsync<ChatInfo>("chat_info", new JsonObject { ["chat"] = chat }, cancellationToken)
            .ConfigureAwait(false);
        return _fence.Wrap("details of a WhatsApp chat", _directory.FormatChatInfo(info));
    }

    public async Task<string> ListStatusesAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var args = includeArchived ? new JsonObject { ["include_archived"] = true } : null;
        var list = await _control.RequestAsync<StatusList>("list_statuses", args, cancellationToken).ConfigureAwait(false);
        return Count(list.Statuses.Count, "status update") + ".\n"
            + _fence.Wrap("WhatsApp status updates", _transcript.FormatStatuses(list.Statuses));
    }

    public async Task<string> ListScheduledAsync(string? chat, CancellationToken cancellationToken)
    {
        var args = string.IsNullOrWhiteSpace(chat) ? null : new JsonObject { ["chat"] = chat };
        var list = await _control.RequestAsync<ScheduledList>("list_scheduled", args, cancellationToken).ConfigureAwait(false);
        return Count(list.Scheduled.Count, "scheduled message") + ".\n"
            + _fence.Wrap("scheduled messages", _directory.FormatScheduled(list.Scheduled));
    }

    public async Task<string> CatchUpPromptAsync(string? since, CancellationToken cancellationToken)
    {
        var summary = await _control.RequestAsync<UnreadSummary>("unread_summary", null, cancellationToken).ConfigureAwait(false);
        var plan = _catchUp.Plan(summary, since);
        return plan.Chats.Count == 0
            ? plan.Instructions
            : plan.Instructions + "\n\n" + _fence.Wrap("the chats with unread messages", _directory.FormatChats(plan.Chats));
    }

    public async Task<string> DraftReplyPromptAsync(string chat, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["chat"] = chat, ["limit"] = DraftContextMessages };
        var page = await _control.RequestAsync<MessagePage>("read_messages", args, cancellationToken).ConfigureAwait(false);
        return _draft.Instructions(page.Chat) + "\n\n" + _fence.Wrap(
            "recent messages from the chat",
            _directory.FormatChat(page.Chat) + "\n\n" + _transcript.FormatMessages(page.Messages));
    }

    private static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : noun.EndsWith("ch", StringComparison.Ordinal) ? "es" : "s")}");
}
