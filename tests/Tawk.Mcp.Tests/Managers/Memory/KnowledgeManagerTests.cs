using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers.Memory;

public class KnowledgeManagerTests
{
    private const string Anneke = "27820000001@s.whatsapp.net";
    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp() => _parts = new MemoryParts();

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public async Task An_observation_is_stored_as_a_concept_about_the_person_and_shown_fenced()
    {
        var result = await _parts.KnowledgeManager.RecordObservationAsync(
            "Anneke Venter", "Starts a new job in March.", "user", ["Work"], null, false, null, null, default);

        var shown = await _parts.KnowledgeManager.GetKnowledgeAsync(Anneke, false, default);
        var concept = (await _parts.Knowledge.ListAsync(OkfTypes.Observation, OkfIds.Contact(Anneke), null, null, default)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.StartWith("Observation observations/" + Anneke + "-").And.EndWith("recorded about Anneke Venter."));
            Assert.That(shown, Does.Contain("Anneke Venter [contacts/" + Anneke + "]").And.Contain("(user) #work: Starts a new job in March."));
            Assert.That(shown, Does.Contain("UNTRUSTED"));
            Assert.That(concept.GeneratedBy, Is.EqualTo("human:self"));
            Assert.That(concept.Verified, Has.Count.EqualTo(1));
            Assert.That(concept.StaleAfter, Is.Null);
        });
    }

    [Test]
    public async Task An_inferred_observation_is_unverified_and_goes_stale()
    {
        await _parts.KnowledgeManager.RecordObservationAsync("Neal Titus", "Seems to prefer voice notes.", "inferred", null, 0.6, false, "m1, m2", null, default);

        var concept = (await _parts.Knowledge.ListAsync(OkfTypes.Observation, null, null, null, default)).Single();
        var listed = await _parts.KnowledgeManager.ListObservationsAsync("Neal Titus", null, null, false, default);
        _parts.Clock.Advance(TimeSpan.FromDays(181));
        var later = await _parts.KnowledgeManager.ListObservationsAsync(null, "general", "voice notes", false, default);

        Assert.Multiple(() =>
        {
            Assert.That(concept.GeneratedBy, Is.EqualTo("tawk-mcp/test"));
            Assert.That(concept.Verified, Is.Empty);
            Assert.That(OkfExtras.Read(concept), Is.EqualTo(new ObservationDetails(FactSource.Inferred, 0.6, false, "m1, m2")));
            Assert.That(listed, Does.Contain("(inferred, 0.60, until ").And.Contain("about Neal Titus #general: Seems to prefer voice notes."));
            Assert.That(later, Does.Contain("(inferred, 0.60, stale)"));
        });
    }

    [Test]
    public async Task Sensitive_observations_come_only_from_the_user_and_stay_hidden_until_asked_for()
    {
        Assert.ThrowsAsync<MemoryException>(() =>
            _parts.KnowledgeManager.RecordObservationAsync("Anneke Venter", "Has been unwell.", "inferred", null, null, true, null, null, default));

        await _parts.KnowledgeManager.RecordObservationAsync("Anneke Venter", "Has been unwell.", "user", ["health"], null, true, null, null, default);
        var hidden = await _parts.KnowledgeManager.GetKnowledgeAsync("Anneke Venter", false, default);
        var shown = await _parts.KnowledgeManager.GetKnowledgeAsync("Anneke Venter", true, default);
        var profile = await _parts.Profiles.GetContactAsync("Anneke Venter", false, default);

        Assert.Multiple(() =>
        {
            Assert.That(hidden, Does.Not.Contain("unwell").And.Contain("1 sensitive observation(s) hidden"));
            Assert.That(shown, Does.Contain("(user, sensitive) #health: Has been unwell."));
            Assert.That(profile, Does.Not.Contain("unwell"));
        });
    }

    [Test]
    public async Task An_observation_shows_among_the_contacts_notes()
    {
        await _parts.KnowledgeManager.RecordObservationAsync("Anneke Venter", "Loves koeksisters.", "contact", null, null, false, null, null, default);

        Assert.That(await _parts.Profiles.GetContactAsync("Anneke Venter", false, default), Does.Contain("(contact): Loves koeksisters."));
    }

    [Test]
    public async Task Relations_join_people_the_user_and_topics_and_show_from_both_ends()
    {
        await _parts.KnowledgeManager.RecordRelationAsync("Anneke Venter", "Spouse  of", "self", "user", null, "since 2010", default);
        await _parts.KnowledgeManager.RecordRelationAsync("Neal Titus", "organiser of", "concepts/cape-town-trip", "inferred", 0.7, null, default);

        var anneke = await _parts.KnowledgeManager.GetKnowledgeAsync("Anneke Venter", false, default);
        var self = await _parts.KnowledgeManager.GetKnowledgeAsync("self", false, default);
        var trip = await _parts.KnowledgeManager.GetKnowledgeAsync("concepts/cape-town-trip", false, default);

        Assert.Multiple(() =>
        {
            Assert.That(anneke, Does.Contain("this is spouse of the user [self] (user): since 2010"));
            Assert.That(self, Does.Contain("Anneke Venter is spouse of this [contacts/" + Anneke + "] (user): since 2010"));
            Assert.That(trip, Does.Contain("cape town trip [concepts/cape-town-trip]").And.Contain("Neal Titus is organiser of this").And.Contain("(inferred, 0.70)"));
        });
    }

    [Test]
    public async Task An_inferred_relation_never_replaces_a_stated_one()
    {
        await _parts.KnowledgeManager.RecordRelationAsync("Neal Titus", "friend of", "self", "user", null, "from school", default);

        var result = await _parts.KnowledgeManager.RecordRelationAsync("Neal Titus", "friend of", "self", "inferred", 0.4, "from work", default);

        Assert.Multiple(async () =>
        {
            Assert.That(result, Does.StartWith("Kept what was already stated by user"));
            Assert.That(await _parts.KnowledgeManager.GetKnowledgeAsync("Neal Titus", false, default), Does.Contain("(user): from school"));
        });
    }

    [Test]
    public async Task Observations_and_relations_can_be_forgotten()
    {
        var recorded = await _parts.KnowledgeManager.RecordObservationAsync("Neal Titus", "Supports the Stormers.", "user", null, null, false, null, null, default);
        var id = recorded.Split(' ')[1];
        await _parts.KnowledgeManager.RecordRelationAsync("Neal Titus", "friend of", "self", "user", null, null, default);

        Assert.Multiple(async () =>
        {
            Assert.That(await _parts.KnowledgeManager.ForgetObservationAsync(id, default), Is.EqualTo($"Forgot observation {id}."));
            Assert.That(await _parts.KnowledgeManager.ForgetRelationAsync("Neal Titus", "friend of", "self", default), Does.StartWith("Forgot that"));
            Assert.That(await _parts.KnowledgeManager.ForgetRelationAsync("Neal Titus", "friend of", "self", default), Does.StartWith("Nothing was stored"));
            Assert.That(await _parts.KnowledgeManager.ListObservationsAsync(null, null, null, false, default), Does.Contain("No observations match."));
        });
        Assert.ThrowsAsync<MemoryException>(() => _parts.KnowledgeManager.ForgetObservationAsync("contacts/" + Anneke, default));
    }

    [Test]
    public void Bad_input_saves_nothing()
    {
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<MemoryException>(() => _parts.KnowledgeManager.RecordObservationAsync("self", " ", "user", null, null, false, null, null, default));
            Assert.ThrowsAsync<MemoryException>(() => _parts.KnowledgeManager.RecordObservationAsync("self", "x", "someone", null, null, false, null, null, default));
            Assert.ThrowsAsync<MemoryException>(() => _parts.KnowledgeManager.RecordObservationAsync("self", "x", "user", ["two words"], null, false, null, null, default));
            Assert.ThrowsAsync<MemoryException>(() => _parts.KnowledgeManager.RecordObservationAsync("concepts/Bad Id", "x", "user", null, null, false, null, null, default));
            Assert.ThrowsAsync<MemoryException>(() => _parts.KnowledgeManager.RecordRelationAsync("self", "about", "Neal Titus", "user", null, null, default));
            Assert.ThrowsAsync<MemoryException>(() => _parts.KnowledgeManager.RecordRelationAsync("self", "same as", "self", "user", null, null, default));
            Assert.ThrowsAsync<TawkControlException>(() => _parts.KnowledgeManager.RecordObservationAsync("Nobody Known", "x", "user", null, null, false, null, null, default));
        });
    }

    [Test]
    public void Read_only_memory_refuses_to_record()
    {
        using var readOnly = new MemoryParts(MemoryMode.Read);

        Assert.ThrowsAsync<MemoryException>(() => readOnly.KnowledgeManager.RecordObservationAsync("self", "x", "user", null, null, false, null, null, default));
    }
}
