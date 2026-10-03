namespace Tawk.Mcp.Core;

/// <summary>A queued message. <see cref="Edited"/> is true when the user changed the text while approving it,
/// and <see cref="Disclaimer"/> when tawk added the user's AI disclaimer under it.</summary>
public sealed record SentMessage(string Id, bool Edited = false, string? Text = null, bool Disclaimer = false);
