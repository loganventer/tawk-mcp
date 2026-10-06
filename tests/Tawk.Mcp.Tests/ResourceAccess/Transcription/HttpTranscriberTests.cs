using System.Net;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class HttpTranscriberTests
{
    private string _file = null!;

    [SetUp]
    public void SetUp()
    {
        _file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ogg");
        File.WriteAllBytes(_file, "OggS-voice"u8.ToArray());
    }

    [TearDown]
    public void TearDown() => File.Delete(_file);

    private static TranscriptionOptions Options => new()
    {
        Engine = TranscriptionEngine.Http,
        Url = new Uri("http://127.0.0.1:8080/"),
    };

    [Test]
    public async Task Sends_the_file_with_the_language_and_reads_the_answer()
    {
        using var handler = new StubHttpHandler(HttpStatusCode.OK, """{"text":" Hallo daar ","language":"afrikaans","duration":4.2}""");
        using var client = new HttpClient(handler);

        var transcript = await new HttpTranscriber(client, Options).TranscribeAsync(
            new TranscriptionPassRequest(_file, "af", TranscriptionTask.Transcribe, "small", "Koos"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Address, Is.EqualTo(new Uri("http://127.0.0.1:8080/v1/audio/transcriptions")));
            Assert.That(handler.Sent, Does.Contain("name=language").And.Contain("af").And.Contain("small").And.Contain("Koos").And.Contain("OggS-voice"));
            Assert.That(transcript, Is.EqualTo(new Transcript("Hallo daar", "afrikaans", "small", 4.2)));
        });
    }

    [Test]
    public async Task Auto_sends_no_language_and_translate_uses_the_translations_address()
    {
        using var handler = new StubHttpHandler(HttpStatusCode.OK, "Hello there\n");
        using var client = new HttpClient(handler);

        var transcript = await new HttpTranscriber(client, Options).TranscribeAsync(
            new TranscriptionPassRequest(_file, "auto", TranscriptionTask.Translate, "base", null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Address!.AbsolutePath, Is.EqualTo("/v1/audio/translations"));
            Assert.That(handler.Sent, Does.Not.Contain("name=language").And.Not.Contain("name=prompt"));
            Assert.That(transcript.Text, Is.EqualTo("Hello there"));
            Assert.That(transcript.Model, Is.EqualTo("base"));
        });
    }

    [Test]
    public void A_refusal_is_a_short_reason()
    {
        using var handler = new StubHttpHandler(HttpStatusCode.InternalServerError, "stack trace ...");
        using var client = new HttpClient(handler);

        Assert.That(
            async () => await new HttpTranscriber(client, Options).TranscribeAsync(
                new TranscriptionPassRequest(_file, "af", TranscriptionTask.Transcribe, "small", null), CancellationToken.None),
            Throws.TypeOf<TranscriptionException>().With.Message.EqualTo("the transcriber answered 500"));
    }
}
