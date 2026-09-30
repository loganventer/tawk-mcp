namespace Tawk.Mcp.Managers.Memory;

/// <summary>A filled-in template ready to put into a chat's draft in tawk.</summary>
public sealed record PreparedDraft(string Jid, string ChatName, string Text, string Notes);
