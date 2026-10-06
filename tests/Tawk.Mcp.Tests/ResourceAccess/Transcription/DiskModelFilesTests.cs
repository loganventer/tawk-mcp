using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class DiskModelFilesTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp() => _folder = Directory.CreateTempSubdirectory("tawk-mcp-models").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task Finds_installed_models_by_name_and_no_others()
    {
        await File.WriteAllBytesAsync(Path.Combine(_folder, "ggml-small.bin"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(_folder, "ggml-evil.bin"), [1]);
        var files = new DiskModelFiles(new TranscriptionOptions { ModelDirectory = _folder });

        Assert.Multiple(() =>
        {
            Assert.That(files.Find("small"), Is.EqualTo(Path.Combine(_folder, "ggml-small.bin")));
            Assert.That(files.Find("tiny"), Is.Null, "not installed");
            Assert.That(files.Find("evil"), Is.Null, "not a model tawk-mcp knows");
            Assert.That(files.Find("../ggml-small"), Is.Null);
            Assert.That(files.Installed(), Is.EqualTo(new[] { "small" }));
        });
    }

    [Test]
    public void An_unknown_name_is_not_fetched()
    {
        var files = new DiskModelFiles(new TranscriptionOptions { ModelDirectory = _folder });

        Assert.That(async () => await files.FetchAsync("gigantic", CancellationToken.None),
            Throws.TypeOf<TranscriptionException>().With.Message.Contains("tiny, base, small"));
    }

    [Test]
    public void The_folder_is_private_by_default_under_the_data_home()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DiskModelFiles.DefaultFolder(_ => null, "/home/logan"), Is.EqualTo("/home/logan/.local/share/tawk-mcp/models"));
            Assert.That(DiskModelFiles.DefaultFolder(k => k == "XDG_DATA_HOME" ? "/data" : null, "/home/logan"), Is.EqualTo("/data/tawk-mcp/models"));
        });
    }
}
