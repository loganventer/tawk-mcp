using System.Globalization;
using System.Text;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public sealed class ChatDirectoryFormatter : IChatDirectoryFormatter
{
    private readonly ITranscriptFormatter _transcript;

    public ChatDirectoryFormatter(ITranscriptFormatter transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        _transcript = transcript;
    }

    public string FormatChats(IEnumerable<ChatSummary> chats)
    {
        ArgumentNullException.ThrowIfNull(chats);
        return string.Join('\n', chats.Select(FormatChat));
    }

    public string FormatChat(ChatSummary chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        var line = new StringBuilder();
        line.Append(OneLine(chat.Name)).Append(" <").Append(chat.Jid).Append('>');
        var flags = new List<string>();
        if (chat.IsGroup)
        {
            flags.Add("group");
        }

        if (chat.Unread > 0)
        {
            flags.Add(string.Create(CultureInfo.InvariantCulture, $"{chat.Unread} unread"));
        }

        if (chat.UnreadMention)
        {
            flags.Add("mentions you");
        }

        if (chat.Pinned)
        {
            flags.Add("pinned");
        }

        if (chat.Muted)
        {
            flags.Add("muted");
        }

        if (chat.Archived)
        {
            flags.Add("archived");
        }

        if (flags.Count > 0)
        {
            line.Append(" (").Append(string.Join(", ", flags)).Append(')');
        }

        if (chat.LastTs > 0)
        {
            line.Append(" last ").Append(_transcript.FormatTimestamp(chat.LastTs));
        }

        if (!string.IsNullOrEmpty(chat.Preview))
        {
            line.Append(": ").Append(OneLine(chat.Preview));
        }

        return line.ToString();
    }

    public string FormatChatInfo(ChatInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var text = new StringBuilder(FormatChat(info.Chat));
        if (!string.IsNullOrWhiteSpace(info.About))
        {
            text.Append("\nAbout: ").Append(OneLine(info.About));
        }

        if (info.Members is { Count: > 0 } members)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nMembers ({members.Count}):");
            foreach (var member in members)
            {
                text.Append("\n- ").Append(OneLine(member.Name ?? member.Jid)).Append(" <").Append(member.Jid).Append('>');
                if (member.Admin)
                {
                    text.Append(" (admin)");
                }
            }
        }

        return text.ToString();
    }

    public string FormatScheduled(IEnumerable<ScheduledItem> scheduled)
    {
        ArgumentNullException.ThrowIfNull(scheduled);
        return string.Join('\n', scheduled.Select(s =>
            $"[due {_transcript.FormatTimestamp(s.DueAt)}] to {s.Chat}: {OneLine(s.Text)} [id {s.Id}]"));
    }

    private static string OneLine(string text) => text.ReplaceLineEndings(" ");
}
