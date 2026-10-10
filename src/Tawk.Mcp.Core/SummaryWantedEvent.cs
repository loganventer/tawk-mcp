namespace Tawk.Mcp.Core;

/// <summary>
/// tawk asks this agent for the TL;DR of a long message, for a chat the user put in TL;DR mode. tawk sends
/// it only to the agent the user chose, or to the only one connected.
/// </summary>
public sealed record SummaryWantedEvent(ChatRef Chat, ChatMessage Message, int MaxChars) : TawkEvent;
