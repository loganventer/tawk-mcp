namespace Tawk.Mcp.Core;

/// <summary>Someone read a message the user sent. <see cref="At"/> is in Unix seconds.</summary>
public sealed record ReadEvent(ChatRef Chat, string MessageId, ReaderRef Reader, long At) : TawkEvent;
