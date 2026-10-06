using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Host;

namespace Tawk.Mcp.Tests.Host;

public class TranscriptionOptionsBinderTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

    [Test]
    public void Transcription_is_off_unless_asked_for()
    {
        var options = TawkMcpOptionsBinder.Bind([], Env());

        Assert.Multiple(() =>
        {
            Assert.That(options.Error, Is.Null);
            Assert.That(options.Transcribe, Is.EqualTo(TranscriptionEngine.Off));
            Assert.That(options.TranscribeMaxLanguages, Is.EqualTo(3));
            Assert.That(options.TranscribeModel, Is.Null, "unset: tawk's settings panel chooses, and the smallest model when it does not");
            Assert.That(options.TranscribeLanguages, Is.Null);
            Assert.That(options.TranscribeAuto, Is.Null);
        });
    }

    [Test]
    public void The_http_engine_takes_its_address_models_and_several_default_languages()
    {
        var options = TawkMcpOptionsBinder.Bind(
            ["--transcribe", "http", "--transcribe-url", "http://localhost:8080", "--transcribe-language", "AF, en,af",
                "--transcribe-model", "base", "--transcribe-models", "small,medium"],
            Env());

        Assert.Multiple(() =>
        {
            Assert.That(options.Error, Is.Null);
            Assert.That(options.Transcribe, Is.EqualTo(TranscriptionEngine.Http));
            Assert.That(options.TranscribeUrl, Is.EqualTo(new Uri("http://localhost:8080")));
            Assert.That(options.TranscribeLanguages, Is.EqualTo(new[] { "af", "en" }));
            Assert.That(options.TranscribeModel, Is.EqualTo("base"));
            Assert.That(options.TranscribeModels, Is.EqualTo(new[] { "small", "medium" }));
        });
    }

    [Test]
    public void A_flag_overrides_the_environment()
    {
        var options = TawkMcpOptionsBinder.Bind(
            ["--transcribe-language", "en"],
            Env(("TAWKMCP_TRANSCRIBE", "command"), ("TAWKMCP_TRANSCRIBE_COMMAND", "whisper {file}"), ("TAWKMCP_TRANSCRIBE_LANGUAGE", "af")));

        Assert.Multiple(() =>
        {
            Assert.That(options.Error, Is.Null);
            Assert.That(options.Transcribe, Is.EqualTo(TranscriptionEngine.Command));
            Assert.That(options.TranscribeCommand, Is.EqualTo("whisper {file}"));
            Assert.That(options.TranscribeLanguages, Is.EqualTo(new[] { "en" }));
        });
    }

    [Test]
    public void A_transcriber_on_another_machine_needs_the_user_to_say_so()
    {
        var refused = TawkMcpOptionsBinder.Bind(["--transcribe", "http", "--transcribe-url", "http://10.0.0.5:8080"], Env());
        var allowed = TawkMcpOptionsBinder.Bind(
            ["--transcribe", "http", "--transcribe-url", "http://10.0.0.5:8080", "--transcribe-remote", "on"], Env());

        Assert.Multiple(() =>
        {
            Assert.That(refused.Error, Does.Contain("must be on this machine"));
            Assert.That(allowed.Error, Is.Null);
        });
    }

    [TestCase("--transcribe", "http")]
    [TestCase("--transcribe", "command")]
    [TestCase("--transcribe", "whisper")]
    [TestCase("--transcribe-url", "ftp://localhost")]
    [TestCase("--transcribe-language", "english")]
    [TestCase("--transcribe-max-languages", "0")]
    public void What_cannot_work_is_refused_at_the_start(string flag, string value)
    {
        Assert.That(TawkMcpOptionsBinder.Bind([flag, value], Env()).Error, Is.Not.Null);
    }

    [Test]
    public void Automatic_transcription_is_a_toggle_that_needs_an_engine()
    {
        var off = TawkMcpOptionsBinder.Bind(["--transcribe", "command", "--transcribe-command", "whisper {file}"], Env());
        var on = TawkMcpOptionsBinder.Bind(
            ["--transcribe", "command", "--transcribe-command", "whisper {file}"], Env(("TAWKMCP_TRANSCRIBE_AUTO", "on")));
        var withoutEngine = TawkMcpOptionsBinder.Bind(["--transcribe-auto", "on"], Env());

        Assert.Multiple(() =>
        {
            Assert.That(off.TranscribeAuto, Is.Null, "unset: the switch in tawk decides");
            Assert.That(on.TranscribeAuto, Is.True);
            Assert.That(on.Error, Is.Null);
            Assert.That(withoutEngine.Error, Does.Contain("needs an engine"));
        });
    }

    [Test]
    public void More_default_languages_than_the_limit_is_refused()
    {
        var options = TawkMcpOptionsBinder.Bind(
            ["--transcribe-language", "af,en,zu", "--transcribe-max-languages", "2"], Env());

        Assert.That(options.Error, Does.Contain("--transcribe-max-languages"));
    }
}
