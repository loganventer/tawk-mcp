namespace Tawk.Mcp.Core;

/// <summary>An event from tawk, with model-ready text for new messages (already fenced as untrusted).</summary>
public sealed record LiveUpdate(TawkEvent Event, string? ModelText = null);
