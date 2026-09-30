using Tawk.Mcp.Core;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class FakeUserConfirmation(ConfirmationAnswer answer) : IUserConfirmation
{
    public List<string> Asked { get; } = [];

    public Task<ConfirmationAnswer> AskAsync(string summary, CancellationToken cancellationToken)
    {
        Asked.Add(summary);
        return Task.FromResult(answer);
    }
}
