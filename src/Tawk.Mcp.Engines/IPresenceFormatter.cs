using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

/// <summary>Says in a line or two whether someone is online, when they were last seen, or why that is not known.</summary>
public interface IPresenceFormatter
{
    string Format(ChatPresence presence);
}
