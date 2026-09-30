namespace Tawk.Mcp.Core;

public sealed record ChatUpdatedEvent(ChatSummary Chat) : TawkEvent;
