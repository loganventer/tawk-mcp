namespace Tawk.Mcp.Clients.Streaming;

/// <summary>One server-sent event: its id, its name and its JSON data on one line.</summary>
public sealed record StreamEvent(long Id, string Name, string Data);
