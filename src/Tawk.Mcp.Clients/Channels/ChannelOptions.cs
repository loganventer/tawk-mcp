namespace Tawk.Mcp.Clients.Channels;

public sealed record ChannelOptions(ChannelMode Mode)
{
    public const string Capability = "claude/channel";
    public const string Method = "notifications/claude/channel";
}
