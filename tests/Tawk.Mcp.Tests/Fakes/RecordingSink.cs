using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class RecordingSink : IEventSink
{
    public List<LiveUpdate> Updates { get; } = [];

    public Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        lock (Updates)
        {
            Updates.Add(update);
        }

        return Task.CompletedTask;
    }
}
