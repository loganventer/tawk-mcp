using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.ResourceAccess.Memory;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Memory;

public class SqliteOkfStoreTests
{
    private const string Jid = "27820000001@s.whatsapp.net";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp() => _parts = new MemoryParts();

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public async Task A_concept_round_trips_with_its_provenance()
    {
        var concept = Concept("concepts/braai", "Concept") with
        {
            Title = "Braai",
            Description = "A fire and meat.",
            Tags = ["food", "weekend"],
            Verified = [new OkfVerification(OkfActors.Self, Now)],
            Status = OkfStatus.Draft,
            StaleAfter = Now.AddDays(30),
            Sources = [new OkfSource("whatsapp:" + Jid, "m1", "a chat", "Anneke", 2, Now)],
            ExtraJson = """{"sensitive":true}""",
            Body = "Every Saturday.",
        };

        await _parts.Knowledge.UpsertAsync(concept, CancellationToken.None);
        var read = await _parts.Knowledge.GetAsync("concepts/braai", CancellationToken.None);

        Assert.That(read, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(read!.Title, Is.EqualTo("Braai"));
            Assert.That(read.Tags, Is.EqualTo(new[] { "food", "weekend" }));
            Assert.That(read.Verified, Is.EqualTo(new[] { new OkfVerification(OkfActors.Self, Now) }));
            Assert.That(read.Status, Is.EqualTo(OkfStatus.Draft));
            Assert.That(read.StaleAfter, Is.EqualTo(Now.AddDays(30)));
            Assert.That(read.Sources, Is.EqualTo(new[] { new OkfSource("whatsapp:" + Jid, "m1", "a chat", "Anneke", 2, Now) }));
            Assert.That(read.ExtraJson, Is.EqualTo("""{"sensitive":true}"""));
            Assert.That(read.Body, Is.EqualTo("Every Saturday."));
        });
    }

    [Test]
    public async Task Concepts_are_found_by_type_tag_text_and_what_they_link_to()
    {
        await _parts.Knowledge.UpsertAsync(Concept("contacts/a", OkfTypes.Contact), CancellationToken.None);
        await _parts.Knowledge.UpsertAsync(Concept("observations/a-1", OkfTypes.Observation) with { Tags = ["work"], Body = "Starts a new job in March." }, CancellationToken.None);
        await _parts.Knowledge.UpsertAsync(Concept("observations/a-2", OkfTypes.Observation) with { Tags = ["family"], Body = "Has 100% of the say." }, CancellationToken.None);
        await _parts.Knowledge.UpsertLinkAsync(Link("observations/a-1", "contacts/a", OkfIds.About), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(Ids(await _parts.Knowledge.ListAsync(OkfTypes.Observation, null, null, null, CancellationToken.None)), Is.EqualTo(new[] { "observations/a-1", "observations/a-2" }));
            Assert.That(Ids(await _parts.Knowledge.ListAsync(null, "contacts/a", null, null, CancellationToken.None)), Is.EqualTo(new[] { "observations/a-1" }));
            Assert.That(Ids(await _parts.Knowledge.ListAsync(null, null, "family", null, CancellationToken.None)), Is.EqualTo(new[] { "observations/a-2" }));
            Assert.That(Ids(await _parts.Knowledge.ListAsync(null, null, null, "new job", CancellationToken.None)), Is.EqualTo(new[] { "observations/a-1" }));
            Assert.That(Ids(await _parts.Knowledge.ListAsync(null, null, null, "100%", CancellationToken.None)), Is.EqualTo(new[] { "observations/a-2" }));
        });
    }

    [Test]
    public async Task A_link_may_point_at_a_concept_that_does_not_exist_yet()
    {
        await _parts.Knowledge.UpsertAsync(Concept("contacts/a", OkfTypes.Contact), CancellationToken.None);
        await _parts.Knowledge.UpsertLinkAsync(Link("contacts/a", "contacts/b", "spouse of") with { Note = "since 2010" }, CancellationToken.None);

        var from = await _parts.Knowledge.GetLinksFromAsync("contacts/a", CancellationToken.None);
        var to = await _parts.Knowledge.GetLinksToAsync("contacts/b", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(from, Has.Count.EqualTo(1));
            Assert.That(from[0].Note, Is.EqualTo("since 2010"));
            Assert.That(to, Is.EqualTo(from));
        });
    }

    [Test]
    public async Task Deleting_a_concept_takes_its_links_and_leaves_a_tombstone_that_a_new_write_clears()
    {
        await _parts.Knowledge.UpsertAsync(Concept("contacts/a", OkfTypes.Contact), CancellationToken.None);
        await _parts.Knowledge.UpsertLinkAsync(Link("contacts/a", "contacts/b", "friend of"), CancellationToken.None);

        var deleted = await _parts.Knowledge.DeleteAsync("contacts/a", CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(await _parts.Knowledge.DeleteAsync("contacts/a", CancellationToken.None), Is.False);
            Assert.That(await _parts.Knowledge.GetLinksToAsync("contacts/b", CancellationToken.None), Is.Empty);
            Assert.That(await TombstonesAsync("okf_concept"), Is.EqualTo(new[] { "contacts/a" }));
        });

        await _parts.Knowledge.UpsertAsync(Concept("contacts/a", OkfTypes.Contact), CancellationToken.None);
        Assert.That(await TombstonesAsync("okf_concept"), Is.Empty);
    }

    [Test]
    public async Task Deleting_a_link_leaves_a_tombstone()
    {
        await _parts.Knowledge.UpsertAsync(Concept("contacts/a", OkfTypes.Contact), CancellationToken.None);
        await _parts.Knowledge.UpsertLinkAsync(Link("contacts/a", "contacts/b", "friend of"), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(await _parts.Knowledge.DeleteLinkAsync("contacts/a", "contacts/b", "friend of", CancellationToken.None), Is.True);
            Assert.That(await TombstonesAsync("okf_link"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task A_contact_has_a_concept_and_its_notes_are_observations_about_it()
    {
        await _parts.Contacts.UpsertAsync(new ContactRecord(Jid, "Anneke", null, Now), CancellationToken.None);
        var first = await _parts.Contacts.AddNoteAsync(new ContactNote(0, Jid, "Likes rooibos.", FactSource.User, Now), CancellationToken.None);
        var second = await _parts.Contacts.AddNoteAsync(new ContactNote(0, Jid, "Seems tired lately.", FactSource.Inferred, Now), CancellationToken.None);

        var contact = await _parts.Knowledge.GetAsync(OkfIds.Contact(Jid), CancellationToken.None);
        var observations = (await _parts.Knowledge.ListAsync(OkfTypes.Observation, OkfIds.Contact(Jid), null, null, CancellationToken.None))
            .OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
        var notes = await _parts.Contacts.GetNotesAsync(Jid, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(contact!.Title, Is.EqualTo("Anneke"));
            Assert.That(contact.Resource, Is.EqualTo("whatsapp:" + Jid));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(observations.Select(o => o.GeneratedBy), Is.EqualTo(new[] { "human:self", "tawk-mcp/test" }));
            Assert.That(observations[0].Verified, Has.Count.EqualTo(1));
            Assert.That(observations[1].Verified, Is.Empty);
            Assert.That(notes.Select(n => (n.Text, n.Source)), Is.EqualTo(new[] { ("Likes rooibos.", FactSource.User), ("Seems tired lately.", FactSource.Inferred) }));
        });
    }

    [Test]
    public async Task Renaming_a_contact_keeps_what_its_concept_holds()
    {
        await _parts.Contacts.UpsertAsync(new ContactRecord(Jid, "Anneke", null, Now), CancellationToken.None);
        var concept = await _parts.Knowledge.GetAsync(OkfIds.Contact(Jid), CancellationToken.None);
        await _parts.Knowledge.UpsertAsync(concept! with { Body = "Met in 2009." }, CancellationToken.None);

        await _parts.Contacts.UpsertAsync(new ContactRecord(Jid, "Anneke V", null, Now.AddHours(1)), CancellationToken.None);
        var after = await _parts.Knowledge.GetAsync(OkfIds.Contact(Jid), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(after!.Title, Is.EqualTo("Anneke V"));
            Assert.That(after.Body, Is.EqualTo("Met in 2009."));
        });
    }

    [Test]
    public async Task Deleting_a_contact_removes_its_concept_and_observations_and_records_it()
    {
        await _parts.Contacts.UpsertAsync(new ContactRecord(Jid, "Anneke", null, Now), CancellationToken.None);
        await _parts.Contacts.AddNoteAsync(new ContactNote(0, Jid, "Likes rooibos.", FactSource.User, Now), CancellationToken.None);

        await _parts.Contacts.DeleteAsync(Jid, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(await _parts.Knowledge.ListAsync(null, null, null, null, CancellationToken.None), Is.Empty);
            Assert.That(await TombstonesAsync("okf_concept"), Has.Count.EqualTo(2));
            Assert.That(await TombstonesAsync("contact"), Is.EqualTo(new[] { Jid }));
        });
    }

    private static OkfConcept Concept(string id, string type) =>
        new(id, type, null, null, null, [], "tawk-mcp/test", Now, [], OkfStatus.Stable, null, [], "{}", string.Empty, Now);

    private static OkfLink Link(string from, string to, string label) => new(from, to, label, null, FactSource.User, 1.0, Now);

    private static string[] Ids(IReadOnlyList<OkfConcept> concepts) => concepts.Select(c => c.Id).Order(StringComparer.Ordinal).ToArray();

    private async Task<List<string>> TombstonesAsync(string kind)
    {
        await using var connection = await _parts.Connections.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key FROM sync_tombstone WHERE kind = $kind ORDER BY key";
        command.Parameters.AddWithValue("$kind", kind);
        var keys = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }
}
