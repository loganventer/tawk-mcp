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

    /// <summary>Runs a use case for one of the user's accounts: every request to tawk made inside names it.</summary>
    public static async Task<CallToolResult> RunAsync(IAccountScope accounts, string? account, Func<Task<string>> action)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        using (accounts.Use(account))
        {
            return await RunAsync(action).ConfigureAwait(false);
        }
    }

    /// <summary>Runs a use case and turns tawk's errors into tool error results instead of exceptions.</summary>
    public static async Task<CallToolResult> RunAsync(Func<Task<string>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return Text(await action().ConfigureAwait(false));
        }
        catch (ApprovalWaitingException ex)
        {
            return Text(TawkServerInstructions.Waiting(ex.RequestId, ex.Op));
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
