namespace Tawk.Mcp.ResourceAccess;

/// <summary>
/// Watches the socket's folder for control.sock. When the folder does not exist yet, watches the nearest
/// existing parent for it to appear and moves down as folders are created.
/// </summary>
public sealed class SocketFileWatcher : ISocketFileWatcher
{
    private readonly Lock _gate = new();
    private FileSystemWatcher? _watcher;
    private string? _watchedFolder;
    private bool _watchingSocketFolder;
    private string? _socketFolder;
    private string? _socketName;
    private Action? _onAppeared;
    private bool _disposed;

    public void Watch(string socketPath, Action onAppeared)
    {
        ArgumentException.ThrowIfNullOrEmpty(socketPath);
        ArgumentNullException.ThrowIfNull(onAppeared);
        var full = Path.GetFullPath(socketPath);
        lock (_gate)
        {
            _socketFolder = Path.GetDirectoryName(full);
            _socketName = Path.GetFileName(full);
            _onAppeared = onAppeared;
            Arm();
        }
    }

    public void Refresh()
    {
        lock (_gate)
        {
            if (_socketFolder is null || _disposed)
            {
                return;
            }

            var stale = _watchedFolder is null
                || !Directory.Exists(_watchedFolder)
                || (!_watchingSocketFolder && Directory.Exists(_socketFolder));
            if (stale)
            {
                Arm();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void Arm()
    {
        if (_disposed || _socketFolder is null)
        {
            return;
        }

        _watcher?.Dispose();
        _watcher = null;
        if (Directory.Exists(_socketFolder))
        {
            var watcher = new FileSystemWatcher(_socketFolder, _socketName!)
            {
                NotifyFilter = NotifyFilters.FileName,
            };
            watcher.Created += (_, _) => _onAppeared?.Invoke();
            watcher.Renamed += (_, _) => _onAppeared?.Invoke();
            watcher.Error += (_, _) => Refresh();
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
            _watchedFolder = _socketFolder;
            _watchingSocketFolder = true;
            if (File.Exists(Path.Combine(_socketFolder, _socketName!)))
            {
                _onAppeared?.Invoke();
            }

            return;
        }

        var ancestor = Path.GetDirectoryName(_socketFolder);
        while (ancestor is not null && !Directory.Exists(ancestor))
        {
            ancestor = Path.GetDirectoryName(ancestor);
        }

        if (ancestor is null)
        {
            return;
        }

        var parentWatcher = new FileSystemWatcher(ancestor)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
        };
        parentWatcher.Created += (_, _) => OnFolderAppeared();
        parentWatcher.Renamed += (_, _) => OnFolderAppeared();
        parentWatcher.Error += (_, _) => Refresh();
        parentWatcher.EnableRaisingEvents = true;
        _watcher = parentWatcher;
        _watchedFolder = ancestor;
        _watchingSocketFolder = false;

        // The folder may have been created between the check and the watch starting.
        if (Directory.Exists(_socketFolder))
        {
            Arm();
        }
    }

    private void OnFolderAppeared()
    {
        lock (_gate)
        {
            Arm();
        }
    }
}
