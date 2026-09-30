using System.Threading.Channels;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>Wakes the supervisor for an immediate connect attempt. Repeated signals collapse into one.</summary>
public sealed class ConnectSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Signal() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
}
