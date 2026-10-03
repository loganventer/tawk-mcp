namespace Tawk.Mcp.Core;

/// <summary>Who read a message: their JID and the name tawk knows them by.</summary>
public sealed record ReaderRef(string Jid, string? Name);
