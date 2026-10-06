using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Runs a callback for every update, to look at the world at that moment.</summary>
public sealed class CallbackSink(Action onUpdate) : IEventSink
{
    public Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        onUpdate();
        return Task.CompletedTask;
    }
}
