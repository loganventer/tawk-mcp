using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Engines.Transcription;

namespace Tawk.Mcp.Tests.Engines.Transcription;

public class TranscriptionPolicyTests
{
    private static TranscriptionPolicy Policy(TranscriptionOptions? options = null) =>
        new(options ?? new TranscriptionOptions { Engine = TranscriptionEngine.Http });

    [Test]
    public void A_call_without_choices_gets_the_users_defaults()
    {
        var request = Policy(new TranscriptionOptions { Languages = ["af", "en"], Model = "small" }).Resolve(" 3EB0 ", null, null, null, null, null, TranscriptionPreferences.None);

        Assert.Multiple(() =>
        {
            Assert.That(request.MessageId, Is.EqualTo("3EB0"));
            Assert.That(request.Languages, Is.EqualTo(new[] { "af", "en" }));
            Assert.That(request.Task, Is.EqualTo(TranscriptionTask.Transcribe));
            Assert.That(request.Model, Is.EqualTo("small"));
            Assert.That(request.Prompt, Is.Null);
        });
    }

    [Test]
    public void The_default_model_is_used_until_the_user_names_another()
    {
        Assert.That(Policy().Resolve("3EB0", null, null, null, null, null, TranscriptionPreferences.None).Model, Is.EqualTo("large-v3-turbo"));
    }

    [Test]
    public void Tawks_settings_panel_overrides_tawk_mcps_own_settings_which_stand_in_when_tawk_says_nothing()
    {
        var panel = new TranscriptionPreferences("small", "af, EN, klingon!", true);
        var followsPanel = Policy();
        var withFlags = Policy(new TranscriptionOptions { Model = "base", Languages = ["zu"], Automatic = false });

        var fromPanel = withFlags.Resolve("3EB0", null, null, null, null, null, panel);
        var fromFlags = withFlags.Resolve("3EB0", null, null, null, null, null, TranscriptionPreferences.None);

        Assert.Multiple(() =>
        {
            Assert.That(fromPanel.Model, Is.EqualTo("small"));
            Assert.That(fromPanel.Languages, Is.EqualTo(new[] { "af", "en" }), "what is not a language is passed over");
            Assert.That(withFlags.Automatic(panel), Is.True, "the switch in tawk wins over tawk-mcp's own");
            Assert.That(fromFlags.Model, Is.EqualTo("base"));
            Assert.That(fromFlags.Languages, Is.EqualTo(new[] { "zu" }));
            Assert.That(Policy(new TranscriptionOptions { Automatic = true }).Automatic(TranscriptionPreferences.None), Is.True);
            Assert.That(followsPanel.Automatic(TranscriptionPreferences.None), Is.False, "nothing is automatic until the user says so");
            Assert.That(followsPanel.Resolve("3EB0", null, null, null, "small", null, panel).Model, Is.EqualTo("small"), "the chosen model may be named");
            Assert.That(() => followsPanel.Resolve("3EB0", null, null, null, "large-v3", null, panel), Throws.TypeOf<TranscriptionException>());
        });
    }

    [Test]
    public void With_transcription_off_every_request_is_refused_saying_so_and_nothing_is_automatic()
    {
        var off = Policy(new TranscriptionOptions { Engine = TranscriptionEngine.Off, Automatic = true });

        Assert.Multiple(() =>
        {
            Assert.That(() => off.Resolve("3EB0", null, null, null, null, null, TranscriptionPreferences.None),
                Throws.TypeOf<TranscriptionException>().With.Message.Contains("--transcribe off"));
            Assert.That(off.Automatic(new TranscriptionPreferences("tiny", "auto", true)), Is.False);
        });
    }

    [Test]
    public void Several_languages_keep_their_order_and_lose_repeats()
    {
        var request = Policy().Resolve("3EB0", null, ["Auto", " af", "EN", "af"], "translate", null, null, TranscriptionPreferences.None);

        Assert.Multiple(() =>
        {
            Assert.That(request.Languages, Is.EqualTo(new[] { "auto", "af", "en" }));
            Assert.That(request.Task, Is.EqualTo(TranscriptionTask.Translate));
        });
    }

    [Test]
    public void More_languages_than_the_user_allows_are_refused()
    {
        var policy = Policy(new TranscriptionOptions { MaxLanguages = 2 });

        Assert.That(() => policy.Resolve("3EB0", null, ["af", "en", "zu"], null, null, null, TranscriptionPreferences.None),
            Throws.TypeOf<TranscriptionException>().With.Message.Contains("At most 2"));
    }

    [TestCase("english")]
    [TestCase("e")]
    [TestCase("en-GB")]
    [TestCase("")]
    public void Something_that_is_not_a_language_code_is_refused(string language)
    {
        Assert.That(() => Policy().Resolve("3EB0", null, [language], null, null, null, TranscriptionPreferences.None), Throws.TypeOf<TranscriptionException>());
    }

    [Test]
    public void Only_models_the_user_allowed_may_be_asked_for()
    {
        var policy = Policy(new TranscriptionOptions { Model = "base", Models = ["small"] });

        Assert.Multiple(() =>
        {
            Assert.That(policy.Resolve("3EB0", null, null, null, "small", null, TranscriptionPreferences.None).Model, Is.EqualTo("small"));
            Assert.That(policy.Resolve("3EB0", null, null, null, "base", null, TranscriptionPreferences.None).Model, Is.EqualTo("base"));
            Assert.That(() => policy.Resolve("3EB0", null, null, null, "large", null, TranscriptionPreferences.None),
                Throws.TypeOf<TranscriptionException>().With.Message.Contains("small"));
        });
    }

    [Test]
    public void A_bad_task_a_long_prompt_and_a_missing_id_are_refused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => Policy().Resolve("3EB0", null, null, "summarise", null, null, TranscriptionPreferences.None), Throws.TypeOf<TranscriptionException>());
            Assert.That(() => Policy().Resolve("3EB0", null, null, null, null, new string('x', 501), TranscriptionPreferences.None), Throws.TypeOf<TranscriptionException>());
            Assert.That(() => Policy().Resolve(" ", null, null, null, null, null, TranscriptionPreferences.None), Throws.TypeOf<TranscriptionException>());
        });
    }

    [Test]
    public void The_same_request_has_the_same_key_and_another_language_set_does_not()
    {
        var one = Policy().Resolve("3EB0", null, ["af", "en"], null, null, null, TranscriptionPreferences.None);
        var same = Policy().Resolve("3EB0", null, ["af", "en"], null, null, null, TranscriptionPreferences.None);
        var other = Policy().Resolve("3EB0", null, ["af"], null, null, null, TranscriptionPreferences.None);

        Assert.Multiple(() =>
        {
            Assert.That(same.Key, Is.EqualTo(one.Key));
            Assert.That(other.Key, Is.Not.EqualTo(one.Key));
        });
    }
}
