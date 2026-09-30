using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines.Memory;

namespace Tawk.Mcp.Tests.Engines.Memory;

public class VoiceResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero);
    private static readonly Voice Logan = new("logan", null, "base", new VoiceRules { Case = "lower", MaxEmoji = 1 }, true, Now);

    [Test]
    public void Walks_up_the_category_path_and_overlays_the_variant_rules()
    {
        VoiceVariant[] variants = [new("logan", "family", "family guide", new VoiceRules { Languages = ["af"], MaxEmoji = 2 }, [], Now)];

        var resolved = new VoiceResolver().Resolve(Logan, variants, ["family/spouse"]);

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Variant!.Category, Is.EqualTo("family"));
            Assert.That(resolved.Rules.MaxEmoji, Is.EqualTo(2));
            Assert.That(resolved.Rules.Case, Is.EqualTo("lower"));
            Assert.That(resolved.Rules.Languages, Is.EqualTo(new[] { "af" }));
        });
    }

    [Test]
    public void Falls_back_to_the_base_voice_when_nothing_matches()
    {
        var resolved = new VoiceResolver().Resolve(Logan, [], ["work/peers"]);

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Variant, Is.Null);
            Assert.That(resolved.Rules, Is.SameAs(Logan.Rules));
        });
    }
}
