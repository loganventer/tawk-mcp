namespace Tawk.Mcp.Core;

/// <summary>A queued message. <see cref="Edited"/> is true when the user changed the text while approving it.</summary>
public sealed record SentMessage(string Id, bool Edited = false, string? Text = null);
