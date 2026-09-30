using ModelContextProtocol.Protocol;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Clients;

public static class ToolResults
{
    public static CallToolResult Text(string text) => new()
    {
        Content = [new TextContentBlock { Text = text }],
        IsError = false,
    };

    public static CallToolResult Error(string text) => new()
    {
        Content = [new TextContentBlock { Text = text }],
        IsError = true,
    };

    /// <summary>Runs a use case and turns tawk's errors into tool error results instead of exceptions.</summary>
    public static async Task<CallToolResult> RunAsync(Func<Task<string>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return Text(await action().ConfigureAwait(false));
        }
        catch (TawkControlException ex)
        {
            return Error(ControlErrorMessages.Describe(ex));
        }
        catch (MemoryException ex)
        {
            return Error(ex.Message);
        }
    }
}
