namespace Tawk.Mcp.Core;

/// <summary>
/// What tawk knows about the person in a one-to-one chat being online. <see cref="State"/> is
/// <c>online</c>, <c>offline</c> or <c>unknown</c>; <see cref="LastSeen"/> is in Unix seconds, where they
/// share it. <see cref="Watching"/> is false while tawk is not shown as online itself, when WhatsApp
/// tells it nothing about anyone.
/// </summary>
public sealed record ChatPresence(ChatSummary Chat, string State, long? LastSeen, bool Watching)
{
    public const string Online = "online";
    public const string Offline = "offline";
    public const string Unknown = "unknown";
}
