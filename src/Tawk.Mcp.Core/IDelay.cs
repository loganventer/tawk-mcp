namespace Tawk.Mcp.Core;

public interface IDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
