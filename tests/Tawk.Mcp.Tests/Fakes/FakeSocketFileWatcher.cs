using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class FakeSocketFileWatcher : ISocketFileWatcher
{
    private Action? _onAppeared;

    public string? WatchedPath { get; private set; }

    public void Watch(string socketPath, Action onAppeared)
    {
        WatchedPath = socketPath;
        _onAppeared = onAppeared;
    }

    public void Refresh()
    {
    }

    public void Appear() => _onAppeared?.Invoke();

    public void Dispose()
    {
    }
}
