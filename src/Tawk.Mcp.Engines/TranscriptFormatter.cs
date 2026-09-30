using System.Globalization;
using System.Text;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public sealed class TranscriptFormatter : ITranscriptFormatter
{
    private const string ContinuationIndent = "    ";
    private readonly TimeZoneInfo _timeZone;

    public TranscriptFormatter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeZone = timeProvider.LocalTimeZone;
    }

    public string FormatMessages(IEnumerable<ChatMessage> messages, bool includeChat = false)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return string.Join('\n', messages.Select(m => FormatMessage(m, includeChat)));
    }

    public string FormatMessage(ChatMessage message, bool includeChat = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        var line = new StringBuilder();
        line.Append('[').Append(FormatTimestamp(message.Ts)).Append("] ");
        line.Append(message.FromMe ? "You" : OneLine(message.SenderName ?? message.Sender ?? "Unknown"));
        if (includeChat)
        {
            line.Append(" in ").Append(message.Chat);
        }

        if (message.ReplyTo is { } reply)
        {
            line.Append(reply.Status ? " (replying to a status of " : " (replying to ")
                .Append(OneLine(reply.Sender ?? "someone"));
            if (!string.IsNullOrEmpty(reply.Text))
            {
                line.Append(": \"").Append(Shorten(OneLine(reply.Text), 80)).Append('"');
            }

            line.Append(')');
        }

        line.Append(": ");
        if (message.Forwarded)
        {
            line.Append("[forwarded] ");
        }

        if (message.Deleted)
        {
            line.Append("[deleted]");
        }
        else
        {
            if (!string.Equals(message.Type, "text", StringComparison.Ordinal))
            {
                line.Append('[').Append(message.Type).Append(']');
                if (!string.IsNullOrEmpty(message.Text))
                {
                    line.Append(' ');
                }
            }

            line.Append(Indent(message.Text ?? string.Empty));
        }

        if (message.Link is { } link)
        {
            line.Append(" <link: ");
            if (!string.IsNullOrWhiteSpace(link.Title))
            {
                line.Append(OneLine(link.Title)).Append(' ');
            }

            line.Append(OneLine(link.Url)).Append('>');
        }

        if (message.Edited)
        {
            line.Append(" (edited)");
        }

        if (!string.IsNullOrEmpty(message.Reactions))
        {
            line.Append(" {reactions: ").Append(OneLine(message.Reactions)).Append('}');
        }

        if (message.MentionsMe)
        {
            line.Append(" (mentions you)");
        }

        if (message.FromMe && !string.IsNullOrEmpty(message.Status))
        {
            line.Append(" (").Append(message.Status).Append(')');
        }

        line.Append(" [id ").Append(message.Id).Append(']');
        return line.ToString();
    }

    public string FormatStatuses(IEnumerable<StatusItem> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        return string.Join('\n', statuses.Select(s =>
        {
            var author = s.FromMe ? "You" : OneLine(s.AuthorName ?? s.Author ?? "Unknown");
            var kind = string.Equals(s.Type, "text", StringComparison.Ordinal) ? string.Empty : $"[{s.Type}] ";
            var viewed = s.Viewed ? string.Empty : " (not viewed)";
            return $"[{FormatTimestamp(s.Ts)}] {author}: {kind}{Indent(s.Text ?? string.Empty)}{viewed} [id {s.Id}]";
        }));
    }

    public string FormatTimestamp(long unixSeconds)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), _timeZone);
        return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    // Continuation lines are indented so text cannot start a line that looks like a new message.
    private static string Indent(string text) =>
        text.ReplaceLineEndings("\n").Replace("\n", "\n" + ContinuationIndent, StringComparison.Ordinal);

    private static string OneLine(string text) => text.ReplaceLineEndings(" ");

    private static string Shorten(string text, int max) => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 3), "...");
}
