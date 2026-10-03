using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Memory;

/// <summary>What memory learns says which of the user's accounts it was learnt through.</summary>
public class SchemaStepThreeTests
{
    private const string Jid = "27820000001@s.whatsapp.net";
    private const string Work = "27830000001@s.whatsapp.net";
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    // A database from before the account column, with one fact in it.
    private const string Before =
        """
        CREATE TABLE category (path TEXT PRIMARY KEY, description TEXT);
        CREATE TABLE contact (jid TEXT PRIMARY KEY, display_name TEXT, voice TEXT, updated INTEGER NOT NULL);
        CREATE TABLE contact_category (
            jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE, category TEXT NOT NULL,
            PRIMARY KEY (jid, category));
        CREATE TABLE contact_fact (
            jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE, field TEXT NOT NULL, value TEXT NOT NULL,
            source TEXT NOT NULL, confidence REAL NOT NULL, evidence TEXT, sensitive INTEGER NOT NULL,
            updated INTEGER NOT NULL, expires INTEGER, PRIMARY KEY (jid, field));
        CREATE TABLE contact_note (
            id INTEGER PRIMARY KEY AUTOINCREMENT, jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE,
            text TEXT NOT NULL, source TEXT NOT NULL, created INTEGER NOT NULL);
        INSERT INTO contact VALUES ('27820000001@s.whatsapp.net', 'Anneke', NULL, 1790000000000);
        INSERT INTO contact_fact VALUES ('27820000001@s.whatsapp.net', 'city', '"Pretoria"', 'user', 1.0, NULL, 0, 1790000000000, NULL);
        PRAGMA user_version = 1;
        """;

    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp() => _parts = new MemoryParts();

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public async Task A_fact_from_before_is_kept_and_left_untagged()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_parts.Connections.Path)!);
        using (var raw = new SqliteConnection("Data Source=" + _parts.Connections.Path))
        {
            raw.Open();
            using var command = raw.CreateCommand();
            command.CommandText = Before;
            command.ExecuteNonQuery();
        }

        var fact = (await _parts.Contacts.GetFactsAsync(Jid, CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(fact.ValueJson, Is.EqualTo("\"Pretoria\""));
            Assert.That(fact.Account, Is.Null);
        });
    }

    [Test]
    public async Task A_fact_and_a_relation_keep_the_account_they_were_learnt_through()
    {
        await _parts.Contacts.UpsertAsync(new ContactRecord(Jid, "Anneke", null, Now), CancellationToken.None);
        await _parts.Contacts.UpsertFactAsync(
            new ContactFact(Jid, "city", "\"Pretoria\"", FactSource.User, 1.0, null, false, Now, null) { Account = Work }, CancellationToken.None);
        await _parts.Knowledge.UpsertAsync(
            new OkfConcept(OkfIds.Contact(Jid), OkfTypes.Contact, "Anneke", null, null, [], OkfActors.Self, Now, [], OkfStatus.Stable, null, [], "{}", string.Empty, Now),
            CancellationToken.None);
        await _parts.Knowledge.UpsertLinkAsync(
            new OkfLink(OkfIds.Contact(Jid), "topics/tea", "likes", null, FactSource.User, 1.0, Now) { Account = Work }, CancellationToken.None);

        var fact = (await _parts.Contacts.GetFactsAsync(Jid, CancellationToken.None)).Single();
        var link = (await _parts.Knowledge.GetLinksFromAsync(OkfIds.Contact(Jid), CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(fact.Account, Is.EqualTo(Work));
            Assert.That(link.Account, Is.EqualTo(Work));
        });
    }

    [Test]
    public void An_observation_carries_its_account_among_its_producer_keys()
    {
        var extra = OkfExtras.Write(new ObservationDetails(FactSource.Inferred, 0.5, false, null) { Account = Work });
        var concept = new OkfConcept("observations/x", OkfTypes.Observation, null, null, null, [], "tawk-mcp/0.3.0", Now, [], OkfStatus.Stable, null, [], extra, "text", Now);

        Assert.Multiple(() =>
        {
            Assert.That(OkfExtras.Read(concept).Account, Is.EqualTo(Work));
            Assert.That(OkfExtras.Read(concept with { ExtraJson = "{}" }).Account, Is.Null);
        });
    }

    [Test]
    public async Task The_tag_is_the_jid_of_the_account_the_call_is_for()
    {
        var tawk = new FakeTawkControl();
        tawk.Hello = tawk.Hello with
        {
            MultiAccount = true,
            DefaultAccount = 1,
            Accounts =
            [
                new AccountSummary(1, "main", "27830000000@s.whatsapp.net", "Logan", true, true, "send"),
                new AccountSummary(2, "work", Work, "Logan", true, false, "read"),
            ],
        };
        var scope = new AmbientAccountScope();
        var tag = new TawkAccountTag(tawk, scope);

        var byDefault = await tag.CurrentAsync(CancellationToken.None);
        string? byLabel, byId, unknown;
        using (scope.Use("Work"))
        {
            byLabel = await tag.CurrentAsync(CancellationToken.None);
        }

        using (scope.Use("2"))
        {
            byId = await tag.CurrentAsync(CancellationToken.None);
        }

        using (scope.Use("nobody"))
        {
            unknown = await tag.CurrentAsync(CancellationToken.None);
        }

        Assert.Multiple(() =>
        {
            Assert.That(byDefault, Is.EqualTo("27830000000@s.whatsapp.net"));
            Assert.That(byLabel, Is.EqualTo(Work));
            Assert.That(byId, Is.EqualTo(Work));
            Assert.That(unknown, Is.Null);
        });
    }
}
