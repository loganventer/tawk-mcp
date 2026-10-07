namespace Tawk.Mcp.Clients.Channels;

/// <summary>
/// Which clients get channel events, and whether the messages the user sends themselves are among them
/// and whether read receipts, reactions, edits and deletes, scheduled sends and online status are (each off unless asked
/// for: by default only what other people send is pushed).
/// </summary>
public sealed record ChannelOptions(
    ChannelMode Mode, bool OwnMessages = false, bool ReadReceipts = false, bool Reactions = false, bool Edits = false, bool Scheduled = false,
    bool Presence = false)
{
    public const string Capability = "claude/channel";
    public const string Method = "notifications/claude/channel";
}
