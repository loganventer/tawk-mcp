using System.Runtime.Versioning;
using Tawk.Mcp.Host;

namespace Tawk.Mcp.Tests.Host;

public class FileTokenStoreTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "tawk-token-" + Guid.NewGuid().ToString("N")[..8]);

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public void Creates_a_random_base64url_token_once_in_private_files()
    {
        var path = Path.Combine(_root, "tawk-mcp", "token");
        var store = new FileTokenStore(path);

        var first = store.GetOrCreate();
        var second = new FileTokenStore(path).GetOrCreate();

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Length.EqualTo(43));
            Assert.That(first, Does.Match("^[A-Za-z0-9_-]+$"));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(File.GetUnixFileMode(path), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
            Assert.That(
                File.GetUnixFileMode(Path.GetDirectoryName(path)!),
                Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
        });
    }

    [Test]
    public void Tokens_differ_every_time()
    {
        Assert.That(FileTokenStore.NewToken(), Is.Not.EqualTo(FileTokenStore.NewToken()));
    }

    [Test]
    public void The_default_path_follows_the_config_folder()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileTokenStore.DefaultPath(_ => null, "/home/me"), Is.EqualTo("/home/me/.config/tawk-mcp/token"));
            Assert.That(FileTokenStore.DefaultPath(k => k == "XDG_CONFIG_HOME" ? "/cfg" : null, "/home/me"), Is.EqualTo("/cfg/tawk-mcp/token"));
        });
    }
}
