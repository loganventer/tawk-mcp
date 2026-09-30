using Tawk.Mcp.Engines.Memory;

namespace Tawk.Mcp.Tests.Engines.Memory;

public class StyleFeatureExtractorTests
{
    private readonly StyleFeatureExtractor _extractor = new();

    [Test]
    public void Measures_case_emoji_ellipses_and_laughter()
    {
        var features = _extractor.Extract("ok ek is op pad... lol :) 🙂");

        Assert.Multiple(() =>
        {
            Assert.That(features.AllLowercase, Is.True);
            Assert.That(features.Emoji, Is.EquivalentTo(new[] { "🙂", ":)" }));
            Assert.That(features.EllipsisCount, Is.EqualTo(1));
            Assert.That(features.HasLaugh, Is.True);
            Assert.That(features.Language, Is.EqualTo("af"));
        });
    }

    [Test]
    public void Tells_afrikaans_english_and_a_mix_apart()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_extractor.Extract("ek dink dis nie reg nie").Language, Is.EqualTo("af"));
            Assert.That(_extractor.Extract("i think that is not right").Language, Is.EqualTo("en"));
            Assert.That(_extractor.Extract("ek dink the build is broken, maar dis fine").Language, Is.EqualTo("mix"));
            Assert.That(_extractor.Extract("boom").Language, Is.EqualTo("unknown"));
        });
    }
}
