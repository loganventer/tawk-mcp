using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers.Memory;

public class TemplateManagerTests
{
    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp() => _parts = new MemoryParts();

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public async Task Fills_contact_placeholders_and_checks_the_voice()
    {
        await _parts.VoiceManager.SetVoiceAsync("logan", null, "guide", """{"case":"lower"}""", null, default);
        await _parts.Profiles.SetFieldsAsync("Neal Titus", """{"nicknames":["oom Neal","Nealie"]}""", "user", null, null, default);
        await _parts.TemplateManager.SetAsync("birthday", "geluk {{contact.nicknames}}! lekker dag {{day}} hoor", null, "friends", null, "af", default);

        var rendered = await _parts.TemplateManager.RenderAsync("birthday", "Neal Titus", """{"day":"vandag"}""", default);
        var missing = await _parts.TemplateManager.RenderAsync("birthday", "Neal Titus", null, default);

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Does.Contain("geluk oom Neal! lekker dag vandag hoor").And.Contain("Voice check: 100/100"));
            Assert.That(missing, Does.Contain("No value for: day"));
        });
    }

    [Test]
    public async Task A_draft_is_refused_while_a_placeholder_is_empty()
    {
        await _parts.TemplateManager.SetAsync("late", "sorry {{contact.first_name}}, ek is {{minutes}} min laat", null, null, null, null, default);

        var draft = await _parts.TemplateManager.PrepareDraftAsync("late", "Anneke Venter", """{"minutes":10}""", default);

        Assert.Multiple(() =>
        {
            Assert.That(draft.Text, Is.EqualTo("sorry Anneke, ek is 10 min laat"));
            Assert.That(draft.Jid, Is.EqualTo("27820000001@s.whatsapp.net"));
            Assert.That(
                async () => await _parts.TemplateManager.PrepareDraftAsync("late", "Anneke Venter", null, default),
                Throws.TypeOf<MemoryException>().With.Message.Contains("minutes"));
        });
    }
}
