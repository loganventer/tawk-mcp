namespace Tawk.Mcp.Core;

/// <summary>The account something happened in: its id in tawk and the label the user gave it.</summary>
public sealed record AccountRef(int Id, string Label);
