namespace Tawk.Mcp.Core.Media;

/// <summary>tawk finished downloading a message's file. <see cref="Type"/> is the message type, such as image or audio.</summary>
public sealed record MediaReadyEvent(ChatRef? Chat, string MessageId, string Path, string? Type) : TawkEvent;
