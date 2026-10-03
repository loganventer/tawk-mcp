namespace Tawk.Mcp.Core;

/// <summary>
/// One of the user's WhatsApp accounts in tawk that agents may use. <see cref="Access"/> is what they may
/// do with it: read, send, manage or admin.
/// </summary>
public sealed record AccountSummary(int Id, string Label, string Jid, string? Name, bool Connected, bool Primary, string Access);
