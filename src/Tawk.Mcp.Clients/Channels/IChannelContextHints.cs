using Tawk.Mcp.Clients.Sessions;

namespace Tawk.Mcp.Clients.Channels;

/// <summary>Decides which channel events carry the pointer to their chat's history.</summary>
public interface IChannelContextHints
{
    /// <summary>
    /// True the first time a session gets an event from a chat, false after: each session is pointed at a
    /// chat's history once, since what an agent knew in an earlier session may be gone in this one.
    /// </summary>
    bool FirstEventOf(IClientSession session, string chatJid);

    /// <summary>Lets go of what was remembered for a session that has ended.</summary>
    void Forget(IClientSession session);
}
