namespace Tawk.Mcp.Core;

public sealed record ReplyRef(string Id, string? Sender, string? Text, bool Status);
