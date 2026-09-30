using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

/// <summary>Writes the short header line announcing a new message to an agent.</summary>
public interface INotificationFormatter
{
    string Header(ChatRef chat, ChatMessage message);
}
