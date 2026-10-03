using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

/// <summary>Writes the short header line announcing a new message to an agent.</summary>
public interface INotificationFormatter
{
    string Header(ChatRef chat, ChatMessage message);

    /// <summary>One line saying who read which of the user's messages.</summary>
    string Read(ChatRef chat, string messageId, ReaderRef reader);
}
