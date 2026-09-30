using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines.Memory;

namespace Tawk.Mcp.Tests.Engines.Memory;

public class VoiceCheckerTests
{
    private static readonly VoiceRules CloseFriends = new()
    {
        Case = "lower",
        MaxWords = 25,
        MaxEmoji = 1,
        Languages = ["mix"],
        ForbiddenPatterns = ["—", @"\bdear\b", "kind regards"],
        Greeting = "optional",
        Signoff = "none",
    };

    private static readonly VoiceRules Elders = new()
    {
        Languages = ["af"],
        RequiredAddressForms = ["oom", "tannie"],
        ForbiddenAddressForms = ["jy", "jou"],
        MustIncludeAny = ["asb", "dankie"],
    };

    private readonly VoiceChecker _checker = new(
        new StyleFeatureExtractor(),
        [
            new ForbiddenPatternRule(), new CaseRule(), new EmojiRule(), new LengthRule(), new LanguageRule(),
            new AddressFormRule(), new RequiredMarkerRule(), new GreetingSignoffRule(), new BaselineDeviationRule(),
        ]);

    [Test]
    public void A_draft_in_the_voice_scores_full_marks()
    {
        var report = _checker.Check("more dude, lekker dag vir jou hoor 🙂", CloseFriends);

        Assert.Multiple(() =>
        {
            Assert.That(report.Findings, Is.Empty);
            Assert.That(report.Score, Is.EqualTo(100));
        });
    }

    [Test]
    public void A_formal_draft_is_flagged_for_the_em_dash_greeting_sign_off_and_case()
    {
        var report = _checker.Check("Dear Sir — I hope you are well.\nKind regards", CloseFriends);
        var rules = report.Findings.Select(f => f.Rule).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rules, Has.Exactly(3).EqualTo("forbidden"));
            Assert.That(rules, Does.Contain("case").And.Contain("signoff"));
            Assert.That(report.Score, Is.LessThan(40));
        });
    }

    [Test]
    public void Addressing_an_elder_as_jy_is_an_error_and_oom_is_expected()
    {
        var report = _checker.Check("Hoe gaan dit met jou vandag?", Elders);

        Assert.Multiple(() =>
        {
            Assert.That(report.Findings.Where(f => f.Rule == "address").Select(f => f.Severity), Does.Contain(FindingSeverity.Error).And.Contain(FindingSeverity.Warning));
            Assert.That(report.Findings.Select(f => f.Rule), Does.Contain("marker"));
            Assert.That(_checker.Check("More oom, dankie vir gister. Hoe gaan dit met oom?", Elders).Findings, Is.Empty);
        });
    }

    [Test]
    public void Emoji_over_the_limit_and_length_are_flagged()
    {
        var report = _checker.Check("lol 😂🤣😃 " + string.Join(' ', Enumerable.Repeat("woord", 60)), CloseFriends);
        var findings = report.Findings.ToDictionary(f => f.Rule, f => f.Severity);

        Assert.Multiple(() =>
        {
            Assert.That(findings["emoji"], Is.EqualTo(FindingSeverity.Error));
            Assert.That(findings["length"], Is.EqualTo(FindingSeverity.Error));
        });
    }

    [Test]
    public void The_wrong_language_is_a_warning_and_mix_allows_either()
    {
        var english = new VoiceRules { Languages = ["af"] };

        Assert.Multiple(() =>
        {
            Assert.That(_checker.Check("thanks so much, that is really good of you", english).Findings.Single().Rule, Is.EqualTo("language"));
            Assert.That(_checker.Check("thanks so much, that is really good of you", CloseFriends).Findings, Is.Empty);
        });
    }

    [Test]
    public void A_bad_pattern_is_skipped_instead_of_failing_the_check()
    {
        var report = _checker.Check("hello", new VoiceRules { ForbiddenPatterns = ["(unclosed"] });

        Assert.That(report.Findings, Is.Empty);
    }

    [Test]
    public void A_learnt_baseline_gives_hints_not_errors()
    {
        var rules = new VoiceRules { Baseline = new StyleBaseline(40, 6, 3, 0.9, 0.1, 0.2, 0.2) };

        var report = _checker.Check("This Is A Much Longer Message Than The User Ever Writes To This Person 😃😃", rules);

        Assert.Multiple(() =>
        {
            Assert.That(report.Findings, Has.Count.EqualTo(3));
            Assert.That(report.Findings.Select(f => f.Severity), Has.All.EqualTo(FindingSeverity.Info));
        });
    }
}
