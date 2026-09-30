using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers.Memory;

public class ContactProfileManagerTests
{
    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp() => _parts = new MemoryParts();

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public async Task Stores_fields_with_their_source_and_shows_them_fenced()
    {
        await _parts.Profiles.SetFieldsAsync("Anneke Venter", """{"relation":"spouse","address_form":"nickname"}""", "user", null, null, default);
        await _parts.Profiles.AddNoteAsync("Anneke Venter", "loves koeksisters", "user", default);

        var profile = await _parts.Profiles.GetContactAsync("27820000001@s.whatsapp.net", false, default);

        Assert.Multiple(() =>
        {
            Assert.That(profile, Does.Contain("relation: \"spouse\" (user)"));
            Assert.That(profile, Does.Contain("loves koeksisters"));
            Assert.That(profile, Does.Contain("UNTRUSTED"));
        });
    }

    [Test]
    public async Task An_inference_never_replaces_what_the_user_said()
    {
        await _parts.Profiles.SetFieldsAsync("Neal Titus", """{"relation":"close-friend"}""", "user", null, null, default);

        var result = await _parts.Profiles.SetFieldsAsync("Neal Titus", """{"relation":"colleague","humour_ok":true}""", "inferred", 0.6, null, default);
        var profile = await _parts.Profiles.GetContactAsync("Neal Titus", false, default);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Saved for Neal Titus: humour_ok").And.Contain("Kept the stronger value for relation"));
            Assert.That(profile, Does.Contain("relation: \"close-friend\" (user)"));
            Assert.That(profile, Does.Contain("humour_ok: true (inferred, 0.60, until 2027-09-30)"));
        });
    }

    [Test]
    public void Sensitive_inferences_and_unknown_fields_save_nothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await _parts.Profiles.SetFieldsAsync("Neal Titus", """{"sensitive_notes":"x","relation":"friend"}""", "inferred", null, null, default),
                Throws.TypeOf<MemoryException>().With.Message.Contains("only the user"));
            Assert.That(
                async () => await _parts.Profiles.SetFieldsAsync("Neal Titus", """{"star_sign":"leo"}""", "user", null, null, default),
                Throws.TypeOf<MemoryException>().With.Message.Contains("not a profile field"));
            Assert.That(
                async () => await _parts.Profiles.SetFieldsAsync("Neal Titus", """{"relation":"friend"}""", "guess", null, null, default),
                Throws.TypeOf<MemoryException>());
        });
    }

    [Test]
    public async Task Sensitive_fields_are_hidden_unless_asked_for()
    {
        await _parts.Profiles.SetFieldsAsync("Oom Stoffel", """{"sensitive_notes":"private","relation":"church"}""", "user", null, null, default);

        Assert.Multiple(async () =>
        {
            Assert.That(await _parts.Profiles.GetContactAsync("Oom Stoffel", false, default), Does.Not.Contain("private").And.Contain("1 sensitive field(s) hidden"));
            Assert.That(await _parts.Profiles.GetContactAsync("Oom Stoffel", true, default), Does.Contain("private"));
        });
    }

    [Test]
    public async Task The_current_situation_lapses_after_a_month()
    {
        await _parts.Profiles.SetFieldsAsync("Neal Titus", """{"current_situation":"moving house"}""", "user", null, null, default);
        _parts.Clock.Advance(TimeSpan.FromDays(31));

        Assert.That(await _parts.Profiles.GetContactAsync("Neal Titus", false, default), Does.Not.Contain("moving house"));
    }

    [Test]
    public async Task Due_follow_ups_are_listed()
    {
        await _parts.Profiles.SetFieldsAsync(
            "Neal Titus",
            """{"follow_ups":[{"text":"ask about the interview","due":"2026-09-30"},{"text":"later","due":"2026-12-01"}]}""",
            "user",
            null,
            null,
            default);

        var due = await _parts.Profiles.DueFollowUpsAsync(0, default);

        Assert.That(due, Does.Contain("ask about the interview").And.Not.Contain("later"));
    }

    [Test]
    public async Task Deleting_needs_the_users_own_yes()
    {
        await _parts.Profiles.AddNoteAsync("Neal Titus", "x", "user", default);

        Assert.Multiple(async () =>
        {
            Assert.That(
                async () => await _parts.Profiles.DeleteContactAsync("Neal Titus", new FakeUserConfirmation(ConfirmationAnswer.Declined), default),
                Throws.TypeOf<MemoryException>());
            Assert.That(
                async () => await _parts.Profiles.DeleteContactAsync("Neal Titus", null, default),
                Throws.TypeOf<MemoryException>().With.Message.Contains("cannot ask"));
            Assert.That(await _parts.Profiles.DeleteContactAsync("Neal Titus", new FakeUserConfirmation(ConfirmationAnswer.Accepted), default), Does.Contain("Forgot"));
        });
    }

    [Test]
    public void A_chat_tawk_does_not_show_is_refused()
    {
        Assert.That(
            async () => await _parts.Profiles.AddNoteAsync("Hidden Person", "x", "user", default),
            Throws.TypeOf<TawkControlException>());
    }

    [Test]
    public void Read_only_memory_refuses_writes()
    {
        using var readOnly = new MemoryParts(MemoryMode.Read);

        Assert.That(
            async () => await readOnly.Profiles.AddNoteAsync("Neal Titus", "x", "user", default),
            Throws.TypeOf<MemoryException>().With.Message.Contains("read-only"));
    }
}
