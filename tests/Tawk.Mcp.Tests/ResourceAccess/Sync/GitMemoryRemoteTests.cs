using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.ResourceAccess.Sync;

namespace Tawk.Mcp.Tests.ResourceAccess.Sync;

/// <summary>The git remote against a real bare repository on disk, standing in for the one reached over SSH.</summary>
[CancelAfter(60000)]
public class GitMemoryRemoteTests
{
    private readonly ProcessGitRunner _git = new(TimeSpan.FromSeconds(30));
    private string _root = null!;
    private string _origin = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tawk-git-" + Guid.NewGuid().ToString("N")[..8]);
        _origin = Path.Combine(_root, "origin.git");
        Directory.CreateDirectory(_root);
        var made = await _git.RunAsync(new GitCommand(["init", "--quiet", "--bare", "--initial-branch", "main", _origin]), CancellationToken.None);
        Assert.That(made.Succeeded, Is.True, made.Error);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);         // git marks its objects read-only
        }

        Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task An_empty_repository_has_no_version_and_the_first_push_creates_the_file()
    {
        var remote = Remote("a");

        var before = await remote.HeadAsync(CancellationToken.None);
        var pushed = await remote.PushAsync(Write("one"), null, "Sync memory", CancellationToken.None);
        var after = await remote.HeadAsync(CancellationToken.None);
        var copy = Path.Combine(_root, "copy");
        await remote.DownloadAsync(after!, copy, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Null);
            Assert.That(pushed, Is.Not.Null.And.EqualTo(after));
            Assert.That(File.ReadAllText(copy), Is.EqualTo("one"));
        });
    }

    [Test]
    public async Task A_push_built_on_an_old_version_is_refused_and_one_built_on_the_new_version_goes_through()
    {
        var first = Remote("a");
        var second = Remote("b");
        var start = await first.PushAsync(Write("one"), await first.HeadAsync(CancellationToken.None), "Sync memory", CancellationToken.None);
        var seen = await second.HeadAsync(CancellationToken.None);

        var moved = await first.PushAsync(Write("two"), start, "Sync memory", CancellationToken.None);
        var late = await second.PushAsync(Write("three"), seen, "Sync memory", CancellationToken.None);
        var now = await second.HeadAsync(CancellationToken.None);
        var retried = await second.PushAsync(Write("three"), now, "Sync memory", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(seen, Is.EqualTo(start));
            Assert.That(moved, Is.Not.Null);
            Assert.That(late, Is.Null);
            Assert.That(now, Is.EqualTo(moved));
            Assert.That(retried, Is.Not.Null);
        });
    }

    [Test]
    public async Task Two_first_pushes_at_once_do_not_overwrite_each_other()
    {
        var first = Remote("a");
        var second = Remote("b");
        await first.HeadAsync(CancellationToken.None);
        await second.HeadAsync(CancellationToken.None);

        var won = await first.PushAsync(Write("one"), null, "Sync memory", CancellationToken.None);
        var lost = await second.PushAsync(Write("two"), null, "Sync memory", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(won, Is.Not.Null);
            Assert.That(lost, Is.Null);
        });
    }

    [Test]
    public async Task Other_files_in_the_repository_are_kept_and_a_branch_without_the_file_counts_as_none()
    {
        var other = new GitMemoryRemote(_git, new SyncOptions { Repository = _origin, File = "README.md" }, Path.Combine(_root, "c.git"), _ => null);
        await other.HeadAsync(CancellationToken.None);
        await other.PushAsync(Write("# notes"), null, "Add notes", CancellationToken.None);
        var remote = Remote("a");

        var before = await remote.HeadAsync(CancellationToken.None);
        var pushed = await remote.PushAsync(Write("one"), before, "Sync memory", CancellationToken.None);
        var readme = Path.Combine(_root, "readme");
        await other.DownloadAsync((await other.HeadAsync(CancellationToken.None))!, readme, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Null);
            Assert.That(pushed, Is.Not.Null);
            Assert.That(File.ReadAllText(readme), Is.EqualTo("# notes"));
        });
    }

    [Test]
    public void A_repository_that_cannot_be_reached_says_so()
    {
        var remote = new GitMemoryRemote(_git, new SyncOptions { Repository = Path.Combine(_root, "missing.git") }, Path.Combine(_root, "d.git"), _ => null);

        var failure = Assert.ThrowsAsync<MemoryException>(async () => await remote.HeadAsync(CancellationToken.None));

        Assert.That(failure!.Message, Does.StartWith("Memory sync could not reach the repository"));
    }

    [Test]
    public void Git_runs_without_prompts_and_uses_the_named_key()
    {
        var chosen = GitSshEnvironment.For(new SyncOptions { KeyFile = "/keys/it's mine" }, _ => null);
        var left = GitSshEnvironment.For(new SyncOptions(), name => name == "GIT_SSH_COMMAND" ? "ssh -F /my/config" : null);
        var plain = GitSshEnvironment.For(new SyncOptions(), _ => null);

        Assert.Multiple(() =>
        {
            Assert.That(chosen["GIT_SSH_COMMAND"], Is.EqualTo("ssh -i '/keys/it'\\''s mine' -o IdentitiesOnly=yes -o BatchMode=yes"));
            Assert.That(left.ContainsKey("GIT_SSH_COMMAND"), Is.False);
            Assert.That(plain["GIT_SSH_COMMAND"], Is.EqualTo("ssh -o BatchMode=yes"));
            Assert.That(plain["GIT_TERMINAL_PROMPT"], Is.EqualTo("0"));
        });
    }

    private GitMemoryRemote Remote(string machine) =>
        new(_git, new SyncOptions { Repository = _origin }, Path.Combine(_root, machine + ".git"), _ => null);

    private string Write(string content)
    {
        var path = Path.Combine(_root, "memory-" + Guid.NewGuid().ToString("N")[..8]);
        File.WriteAllText(path, content);
        return path;
    }
}
