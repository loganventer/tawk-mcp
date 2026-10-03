namespace Tawk.Mcp.Clients.Channels;

/// <summary>
/// Which clients get channel events, and whether the messages the user sends themselves are among them
/// and whether read receipts for the user's messages are (both off unless asked for: by default only what
/// other people send is pushed).
/// </summary>
public sealed record ChannelOptions(ChannelMode Mode, bool OwnMessages = false, bool ReadReceipts = false)
{
    public const string Capability = "claude/channel";
    public const string Method = "notifications/claude/channel";
}
