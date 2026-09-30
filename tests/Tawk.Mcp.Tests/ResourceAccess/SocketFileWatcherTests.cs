using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.ResourceAccess;

[CancelAfter(10000)]
public class SocketFileWatcherTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tawk-watch-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, true);

    [Test]
    public async Task Notices_the_socket_even_when_its_folders_do_not_exist_yet()
    {
        var socket = Path.Combine(_root, "run", "tawk", "control.sock");
        var appeared = new TaskCompletionSource();
        using var watcher = new SocketFileWatcher();
        watcher.Watch(socket, () => appeared.TrySetResult());

        Directory.CreateDirectory(Path.Combine(_root, "run"));
        await Task.Delay(100);
        Directory.CreateDirectory(Path.Combine(_root, "run", "tawk"));
        await Task.Delay(100);
        await File.WriteAllTextAsync(socket, string.Empty);

        await appeared.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(appeared.Task.IsCompletedSuccessfully, Is.True);
    }
}
