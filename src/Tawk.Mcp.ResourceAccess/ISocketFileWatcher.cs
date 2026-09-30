namespace Tawk.Mcp.ResourceAccess;

public interface ISocketFileWatcher : IDisposable
{
    /// <summary>Calls <paramref name="onAppeared"/> whenever the socket file is created.</summary>
    void Watch(string socketPath, Action onAppeared);

    /// <summary>Re-arms the watch if the folder it was watching has gone or has since appeared.</summary>
    void Refresh();
}
