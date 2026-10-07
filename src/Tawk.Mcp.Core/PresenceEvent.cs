namespace Tawk.Mcp.Core;

/// <summary>
/// The person in a one-to-one chat came online or left. <see cref="LastSeen"/> is when they were last on
/// WhatsApp, in Unix seconds, where they share it; <see cref="At"/> is when tawk heard of the change.
/// </summary>
public sealed record PresenceEvent(ChatRef Chat, ReaderRef Who, bool Online, long? LastSeen, long At) : TawkEvent;
