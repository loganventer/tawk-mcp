namespace Tawk.Mcp.Core.Memory;

public sealed record ContactRecord(string Jid, string? DisplayName, string? Voice, DateTimeOffset Updated);
