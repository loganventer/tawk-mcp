using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers.Memory;

public class VoiceManagerTests
{
    private const string Guide = """
        # Voice
        Lowercase, short, "dude".

        ### Wife
        liefie, my vrou

        ### Elders
        oom and tannie, never jy
        """;

    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp() => _parts = new MemoryParts();

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public async Task An_imported_guide_is_used_for_a_contact_through_its_category()
    {
        var imported = await _parts.VoiceManager.ImportVoiceAsync(
            "Logan", Guide, new Dictionary<string, string> { ["Wife"] = "family/spouse", ["Elders"] = "elders" }, null, true, default);
        await _parts.VoiceManager.SetVariantAsync("logan", "elders", null, """{"forbidden_address_forms":["jy"],"required_address_forms":["oom","tannie"]}""", null, default);
        await _parts.Profiles.SetCategoriesAsync("Oom Stoffel", ["elders"], null, default);

        var check = await _parts.VoiceManager.CheckVoiceAsync("Hoe gaan dit met jy?", "Oom Stoffel", null, null, default);
        var guide = await _parts.VoiceManager.GetVoiceAsync(null, null, "Anneke Venter", default);

        Assert.Multiple(() =>
        {
            Assert.That(imported, Does.Contain("2 audience variant(s)"));
            Assert.That(check, Does.Contain("for elders").And.Contain("[error] address"));
            Assert.That(guide, Does.Contain("no audience variant matched"), "Anneke has no category yet");
        });
    }

    [Test]
    public async Task Rules_with_a_typo_are_refused()
    {
        await _parts.VoiceManager.SetVoiceAsync("logan", null, "guide", null, null, default);

        Assert.That(
            async () => await _parts.VoiceManager.SetVoiceAsync("logan", null, null, """{"max_wrds":10}""", null, default),
            Throws.TypeOf<MemoryException>().With.Message.Contains("not valid"));
    }

    [Test]
    public async Task Learning_keeps_numbers_not_messages()
    {
        _parts.Chats.Add("27820000009@s.whatsapp.net", "Chris", "more dude", "lol ja nee", "sien jou netnou 🙂", "cool stuff", "ok boom");
        await _parts.VoiceManager.SetVoiceAsync("logan", null, "guide", null, null, default);

        var result = await _parts.VoiceManager.LearnVoiceAsync("logan", "friends/close", ["Chris"], 50, default);
        var variant = (await _parts.Voices.ListVariantsAsync("logan", default)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Learnt from 5"));
            Assert.That(variant.Rules.Baseline!.LowercaseShare, Is.EqualTo(1));
            Assert.That(variant.Guide, Does.Not.Contain("dude"));
        });
    }

    [Test]
    public async Task Draft_guidance_is_empty_without_voices_and_fenced_with_them()
    {
        Assert.That(await _parts.VoiceManager.ForChatAsync("Neal Titus", default), Is.Empty);

        await _parts.VoiceManager.SetVoiceAsync("logan", null, "lowercase please", null, null, default);

        Assert.That(await _parts.VoiceManager.ForChatAsync("Neal Titus", default), Does.Contain("check_voice").And.Contain("UNTRUSTED").And.Contain("lowercase please"));
    }

    [Test]
    public async Task Deleting_a_voice_asks_first()
    {
        await _parts.VoiceManager.SetVoiceAsync("logan", null, "guide", null, null, default);
        var asked = new FakeUserConfirmation(ConfirmationAnswer.Accepted);

        await _parts.VoiceManager.DeleteVoiceAsync("logan", null, asked, default);

        Assert.Multiple(async () =>
        {
            Assert.That(asked.Asked.Single(), Does.Contain("delete the voice logan"));
            Assert.That(await _parts.Voices.ListAsync(default), Is.Empty);
        });
    }
}
