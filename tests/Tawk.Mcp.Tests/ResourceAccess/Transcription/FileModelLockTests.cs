using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class FileModelLockTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp() => _folder = Directory.CreateTempSubdirectory("tawk-mcp-models").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task Only_one_holder_at_a_time_and_the_next_gets_it_when_the_first_lets_go()
    {
        var path = Path.Combine(_folder, "sub", ".loaded.lock");
        var first = new FileModelLock(path, TimeProvider.System);
        var second = new FileModelLock(path, TimeProvider.System);

        var held = await first.AcquireAsync(TimeSpan.Zero, CancellationToken.None);
        var refused = await second.AcquireAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None);
        held!.Dispose();
        var after = await second.AcquireAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(held, Is.Not.Null);
            Assert.That(refused, Is.Null, "another process must not load a second model");
            Assert.That(after, Is.Not.Null);
        });
        after!.Dispose();
    }
}
