namespace Tawk.Mcp.Clients.Channels;

/// <summary>Decides which channel events carry the pointer to their chat's history.</summary>
public interface IChannelContextHints
{
    /// <summary>
    /// True the first time a chat is asked about, false after: the agent is pointed at a chat's history once,
    /// and trusted to know the chat from then on.
    /// </summary>
    bool FirstEventOf(string chatJid);
}
