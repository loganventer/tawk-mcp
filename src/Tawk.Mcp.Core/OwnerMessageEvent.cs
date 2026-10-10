namespace Tawk.Mcp.Core;

/// <summary>
/// The user wrote this in the owner's chat: the "message yourself" chat they named in tawk as their chat
/// with the agent. tawk decides which messages there are the user's and sends only those as this event, so it
/// is the one event whose text is the user's own words and not untrusted data.
/// </summary>
public sealed record OwnerMessageEvent(ChatRef Chat, ChatMessage Message) : TawkEvent;
