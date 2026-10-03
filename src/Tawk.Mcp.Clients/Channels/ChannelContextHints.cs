namespace Tawk.Mcp.Clients.Channels;

/// <summary>Remembers which chats have already had an event while this server ran, up to a cap.</summary>
public sealed class ChannelContextHints(int capacity = ChannelContextHints.DefaultCapacity) : IChannelContextHints
{
    public const int DefaultCapacity = 1024;

    private readonly Lock _gate = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public bool FirstEventOf(string chatJid)
    {
        ArgumentNullException.ThrowIfNull(chatJid);
        lock (_gate)
        {
            // Past the cap everything is forgotten at once: a hint too many is harmless, an endless set is not.
            if (_seen.Count >= capacity)
            {
                _seen.Clear();
            }

            return _seen.Add(chatJid);
        }
    }
}
