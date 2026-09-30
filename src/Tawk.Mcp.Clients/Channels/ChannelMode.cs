namespace Tawk.Mcp.Clients.Channels;

public enum ChannelMode
{
    /// <summary>Send to sessions whose client identifies as Claude Code.</summary>
    Auto,

    /// <summary>Send to every connected session.</summary>
    On,

    Off,
}
