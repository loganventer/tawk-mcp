using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Fails to connect a set number of times, then connects and stays connected until told to drop.</summary>
public sealed class FakeTawkConnector(int failuresBeforeSuccess) : ITawkConnector
{
    private TaskCompletionSource _disconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _remainingFailures = failuresBeforeSuccess;

    public string SocketPath => "/run/test/tawk/control.sock";

    public int Attempts { get; private set; }

    public TaskCompletionSource Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Func<bool>? SucceedWhen { get; set; }

    public Task<HelloInfo> ConnectOnceAsync(CancellationToken cancellationToken)
    {
        Attempts++;
        var allowed = SucceedWhen?.Invoke() ?? true;
        if (_remainingFailures > 0 || !allowed)
        {
            _remainingFailures--;
            throw TawkControlException.NotRunning(SocketPath);
        }

        Connected.TrySetResult();
        return Task.FromResult(new HelloInfo(1, "0.6.4", "send", null, true));
    }

    public Task WaitForDisconnectAsync(CancellationToken cancellationToken) => _disconnect.Task.WaitAsync(cancellationToken);

    public void PublishStateIfChanged(HelloInfo? hello = null)
    {
    }

    public void Drop()
    {
        var old = _disconnect;
        _disconnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        old.TrySetResult();
    }
}
