using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Engines.Transcription;

namespace Tawk.Mcp.Tests.Engines;

/// <summary>The readings are what Whisper large-v3 gave for real voice notes, rounded.</summary>
public class SpokenLanguageRuleTests
{
    private readonly SpokenLanguageRule _rule = new();

    [Test]
    public void A_language_the_engine_is_sure_of_is_used()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_rule.Choose([new LanguageReading("af", 0.89f, "af", 0.89f)]), Is.EqualTo("af"));
            Assert.That(_rule.Choose([new LanguageReading("en", 0.69f, "tl", 0.08f)]), Is.EqualTo("en"), "English, with nothing else heard with any weight");
        });
    }

    [Test]
    public void A_note_that_mixes_another_language_with_english_is_written_in_the_other_language()
    {
        // Afrikaans with English in it: told it is English, the engine would write a translation.
        Assert.Multiple(() =>
        {
            Assert.That(_rule.Choose([new LanguageReading("en", 0.55f, "af", 0.31f)]), Is.EqualTo("af"));
            Assert.That(_rule.Choose([new LanguageReading("af", 0.66f, "af", 0.66f)]), Is.EqualTo("af"));
        });
    }

    [Test]
    public void Every_stretch_of_a_long_note_counts()
    {
        var english_start_then_afrikaans = new[]
        {
            new LanguageReading("en", 0.80f, "af", 0.04f),
            new LanguageReading("af", 0.70f, "af", 0.70f),
            new LanguageReading("en", 0.60f, "af", 0.25f),
        };
        var english_throughout = new[]
        {
            new LanguageReading("en", 0.90f, "de", 0.02f),
            new LanguageReading("en", 0.85f, "nl", 0.05f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(_rule.Choose(english_start_then_afrikaans), Is.EqualTo("af"));
            Assert.That(_rule.Choose(english_throughout), Is.EqualTo("en"));
        });
    }

    [Test]
    public void Among_other_languages_the_one_heard_most_wins()
    {
        var readings = new[]
        {
            new LanguageReading("nl", 0.40f, "nl", 0.40f),
            new LanguageReading("af", 0.70f, "af", 0.70f),
            new LanguageReading("af", 0.50f, "af", 0.50f),
        };

        Assert.That(_rule.Choose(readings), Is.EqualTo("af"));
    }

    [Test]
    public void With_nothing_heard_the_engine_is_left_to_it()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_rule.Choose([]), Is.Null);
            Assert.That(_rule.Choose([new LanguageReading(null, 0f, null, 0f)]), Is.Null);
        });
    }
}
