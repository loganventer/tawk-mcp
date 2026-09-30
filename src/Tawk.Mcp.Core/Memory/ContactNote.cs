namespace Tawk.Mcp.Core.Memory;

public sealed record ContactNote(long Id, string Jid, string Text, FactSource Source, DateTimeOffset Created);
