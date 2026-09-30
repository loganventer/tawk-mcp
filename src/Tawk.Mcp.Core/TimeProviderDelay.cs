namespace Tawk.Mcp.Core;

public sealed class TimeProviderDelay(TimeProvider timeProvider) : IDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, timeProvider, cancellationToken);
}
