using Tawk.Mcp.Clients.Sessions;

namespace Tawk.Mcp.Clients.Channels;

/// <summary>Remembers, per session, which chats have already had an event, up to a cap per session.</summary>
public sealed class ChannelContextHints(int capacity = ChannelContextHints.DefaultCapacity) : IChannelContextHints
{
    public const int DefaultCapacity = 1024;

    private readonly Lock _gate = new();
    private readonly Dictionary<IClientSession, HashSet<string>> _seen = [];

    public bool FirstEventOf(IClientSession session, string chatJid)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(chatJid);
        lock (_gate)
        {
            if (!_seen.TryGetValue(session, out var chats))
            {
                chats = new HashSet<string>(StringComparer.Ordinal);
                _seen[session] = chats;
            }

            // Past the cap a session's chats are forgotten at once: a hint too many is harmless, an endless set is not.
            if (chats.Count >= capacity)
            {
                chats.Clear();
            }

            return chats.Add(chatJid);
        }
    }

    public void Forget(IClientSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
        {
            _seen.Remove(session);
        }
    }
}
