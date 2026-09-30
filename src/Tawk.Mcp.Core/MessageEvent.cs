namespace Tawk.Mcp.Core;

public sealed record MessageEvent(ChatRef Chat, ChatMessage Message) : TawkEvent;
