using Tawk.Mcp.Engines.Memory;

namespace Tawk.Mcp.Tests.Engines.Memory;

public class VoiceGuideImporterTests
{
    private const string Guide = """
        # Logan's Voice

        Short and warm.

        ## 3. By audience

        ### Wife (Anneke)
        - liefie, my vrou

        ### Close friends
        - dude. you good?

        ## 4. Recurring moves
        boom, kachow
        """;

    [Test]
    public void Mapped_sections_become_variants_and_the_rest_stays_in_the_base_guide()
    {
        var split = new VoiceGuideImporter().Split(Guide, new Dictionary<string, string>
        {
            ["wife (anneke)"] = "family/spouse",
            ["Close  friends"] = "friends/close",
            ["Parents"] = "family/parents",
        });

        Assert.Multiple(() =>
        {
            Assert.That(split.Sections.Select(s => s.Category), Is.EqualTo(new[] { "family/spouse", "friends/close" }));
            Assert.That(split.Sections[0].Guide, Is.EqualTo("- liefie, my vrou"));
            Assert.That(split.Sections[1].Guide, Is.EqualTo("- dude. you good?"));
            Assert.That(split.BaseGuide, Does.Contain("Short and warm.").And.Contain("## 4. Recurring moves").And.Contain("kachow"));
            Assert.That(split.BaseGuide, Does.Not.Contain("liefie"));
            Assert.That(split.UnmatchedHeadings, Is.EqualTo(new[] { "Parents" }));
        });
    }
}
