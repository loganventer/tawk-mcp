using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class OggOpusDecoderTests
{
    private string _file = null!;

    [SetUp]
    public void SetUp() => _file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [TearDown]
    public void TearDown() => File.Delete(_file);

    [Test]
    public async Task A_file_that_is_not_ogg_is_refused_by_what_it_holds_not_by_its_name()
    {
        await File.WriteAllBytesAsync(_file, "ID3 this is an mp3"u8.ToArray());

        Assert.That(() => new OggOpusDecoder().Decode(_file, 600),
            Throws.TypeOf<TranscriptionException>().With.Message.Contains("unsupported format"));
    }

    [Test]
    public async Task A_broken_ogg_file_and_a_missing_one_fail_with_a_short_reason()
    {
        await File.WriteAllBytesAsync(_file, "OggS and then nothing that makes sense"u8.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(() => new OggOpusDecoder().Decode(_file, 600), Throws.TypeOf<TranscriptionException>());
            Assert.That(() => new OggOpusDecoder().Decode(_file + ".gone", 600), Throws.TypeOf<TranscriptionException>());
        });
    }
}
