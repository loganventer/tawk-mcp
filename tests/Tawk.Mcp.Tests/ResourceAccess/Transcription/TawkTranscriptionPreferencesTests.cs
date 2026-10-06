using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class TawkTranscriptionPreferencesTests
{
    private const string Settings = """
        {"settings":[
          {"section":"appearance","key":"transcribe_model","kind":"string","value":"wrong section"},
          {"section":"automation","key":"access","kind":"choice","value":"send"},
          {"section":"automation","key":"transcribe_model","kind":"choice","value":"small","choices":"tiny|base|small|medium|large-v3-turbo|large-v3","changeable":false},
          {"section":"automation","key":"transcribe_languages","kind":"string","value":"af,en","changeable":false},
          {"section":"automation","key":"transcribe_auto","kind":"bool","value":"true","changeable":false}
        ]}
        """;

    private readonly ManualTimeProvider _clock = new();

    [Test]
    public async Task Reads_the_model_the_languages_and_the_switch_from_tawks_settings()
    {
        var control = new FakeTawkControl().Answer("get_settings", Settings);

        var chosen = await new TawkTranscriptionPreferences(control, _clock).ReadAsync(CancellationToken.None);

        Assert.That(chosen, Is.EqualTo(new TranscriptionPreferences("small", "af,en", true)));
    }

    [Test]
    public async Task Asks_tawk_again_only_after_a_few_seconds()
    {
        var control = new FakeTawkControl().Answer("get_settings", Settings);
        var preferences = new TawkTranscriptionPreferences(control, _clock);

        await preferences.ReadAsync(CancellationToken.None);
        await preferences.ReadAsync(CancellationToken.None);
        var asksAtFirst = control.Requests.Count;
        _clock.Advance(TimeSpan.FromSeconds(11));
        await preferences.ReadAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(asksAtFirst, Is.EqualTo(1));
            Assert.That(control.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task An_older_tawk_and_a_tawk_that_is_down_say_nothing()
    {
        var old = new FakeTawkControl().Answer("get_settings", """{"settings":[{"section":"automation","key":"access","value":"send"}]}""");
        var down = new FakeTawkControl().Fail("get_settings", TawkControlException.NotRunning());

        Assert.Multiple(async () =>
        {
            Assert.That(await new TawkTranscriptionPreferences(old, _clock).ReadAsync(CancellationToken.None), Is.EqualTo(TranscriptionPreferences.None));
            Assert.That(await new TawkTranscriptionPreferences(down, _clock).ReadAsync(CancellationToken.None), Is.EqualTo(TranscriptionPreferences.None));
        });
    }
}
