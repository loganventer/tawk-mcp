using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Managers.Memory;

/// <summary>Asks the user directly before memory is deleted. The model cannot answer for them.</summary>
internal static class MemoryConfirmations
{
    public static async Task EnsureAsync(IUserConfirmation? confirmation, string summary, CancellationToken cancellationToken)
    {
        var answer = confirmation is null
            ? ConfirmationAnswer.NotSupported
            : await confirmation.AskAsync(summary, cancellationToken).ConfigureAwait(false);
        switch (answer)
        {
            case ConfirmationAnswer.Accepted:
                return;
            case ConfirmationAnswer.NotSupported:
                throw new MemoryException("This deletes memory, so the user must confirm it, and their MCP client cannot ask them. Nothing was deleted.");
            default:
                throw new MemoryException("The user did not confirm, so nothing was deleted. Do not retry unless they ask.");
        }
    }
}
