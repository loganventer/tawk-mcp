namespace Tawk.Mcp.Engines.Media;

/// <summary>Tells what kind of picture a file is from its first bytes, never from its name.</summary>
public interface IMediaTypeSniffer
{
    /// <summary>The MIME type of a picture an MCP client can show, or null for anything else.</summary>
    string? ImageType(ReadOnlySpan<byte> content);
}
