namespace Tawk.Mcp.Core.Media;

/// <summary>A picture ready to hand to an MCP client.</summary>
public sealed record ImageData(ReadOnlyMemory<byte> Bytes, string MimeType);
