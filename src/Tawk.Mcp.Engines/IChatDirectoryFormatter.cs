using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

/// <summary>Turns chats, chat details and scheduled messages into compact text.</summary>
public interface IChatDirectoryFormatter
{
    string FormatChats(IEnumerable<ChatSummary> chats);

    string FormatChat(ChatSummary chat);

    string FormatChatInfo(ChatInfo info);

    string FormatScheduled(IEnumerable<ScheduledItem> scheduled);
}
