namespace Tawk.Mcp.Core;

/// <summary>The user's labels: every one in use, or those of one chat when <see cref="Chat"/> is given.</summary>
public sealed record LabelList(IReadOnlyList<string> Labels, ChatRef? Chat = null);
