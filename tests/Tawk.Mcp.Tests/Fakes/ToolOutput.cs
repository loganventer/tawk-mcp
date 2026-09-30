using ModelContextProtocol.Protocol;

namespace Tawk.Mcp.Tests.Fakes;

public static class ToolOutput
{
    public static string Text(CallToolResult result) =>
        string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text));
}
