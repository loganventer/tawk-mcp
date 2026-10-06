using Tawk.Mcp.Core.Media;
using Tawk.Mcp.ResourceAccess.Media;

namespace Tawk.Mcp.Tests.ResourceAccess.Media;

public class DiskMediaFilesTests
{
    private readonly DiskMediaFiles _files = new();
    private string _folder = null!;

    [SetUp]
    public void SetUp() => _folder = Directory.CreateTempSubdirectory("tawk-mcp-media").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task Reads_a_plain_file_within_the_limit()
    {
        var path = Path.Combine(_folder, "a.jpg");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        var bytes = await _files.ReadAsync(path, 10, CancellationToken.None);

        Assert.That(bytes.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task Refuses_a_missing_file_a_folder_a_link_a_big_file_and_a_relative_path()
    {
        var real = Path.Combine(_folder, "real.jpg");
        await File.WriteAllBytesAsync(real, [1, 2, 3]);
        var link = Path.Combine(_folder, "link.jpg");
        File.CreateSymbolicLink(link, real);

        Assert.Multiple(() =>
        {
            Assert.That(() => _files.Check(Path.Combine(_folder, "none.jpg"), 10), Throws.TypeOf<MediaException>());
            Assert.That(() => _files.Check(_folder, 10), Throws.TypeOf<MediaException>());
            Assert.That(() => _files.Check(link, 10), Throws.TypeOf<MediaException>());
            Assert.That(() => _files.Check(real, 2), Throws.TypeOf<MediaException>().With.Message.Contains("limit"));
            Assert.That(() => _files.Check("real.jpg", 10), Throws.TypeOf<MediaException>());
        });
    }
}
