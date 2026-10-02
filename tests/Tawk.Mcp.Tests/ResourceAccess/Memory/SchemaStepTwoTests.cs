using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Memory;

/// <summary>A database written by the first schema, opened by this one.</summary>
public class SchemaStepTwoTests
{
    private const string Jid = "27820000001@s.whatsapp.net";

    // The parts of schema 1 that step 2 reads, as the first release created them.
    private const string VersionOne =
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
        INSERT INTO contact_note (jid, text, source, created) VALUES
            ('27820000001@s.whatsapp.net', 'Likes "rooibos".', 'user', 1790000000123),
            ('27820000001@s.whatsapp.net', 'Seems tired lately.', 'inferred', 1790000005000);
        PRAGMA user_version = 1;
        """;

    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp()
    {
        _parts = new MemoryParts();
        Directory.CreateDirectory(Path.GetDirectoryName(_parts.Connections.Path)!);
        using var connection = new SqliteConnection("Data Source=" + _parts.Connections.Path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = VersionOne;
        command.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public async Task Every_contact_gets_a_concept()
    {
        var concept = await _parts.Knowledge.GetAsync(OkfIds.Contact(Jid), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(concept!.Type, Is.EqualTo(OkfTypes.Contact));
            Assert.That(concept.Title, Is.EqualTo("Anneke"));
            Assert.That(concept.Resource, Is.EqualTo("whatsapp:" + Jid));
        });
    }

    [Test]
    public async Task Every_note_becomes_an_observation_about_its_contact()
    {
        var notes = await _parts.Contacts.GetNotesAsync(Jid, CancellationToken.None);
        var observations = await _parts.Knowledge.ListAsync(OkfTypes.Observation, OkfIds.Contact(Jid), null, null, CancellationToken.None);
        var mine = observations.Single(o => o.Id == "observations/" + Jid + "-1790000000123");

        Assert.Multiple(() =>
        {
            Assert.That(notes.Select(n => (n.Text, n.Source)), Is.EqualTo(new[] { ("Likes \"rooibos\".", FactSource.User), ("Seems tired lately.", FactSource.Inferred) }));
            Assert.That(notes[0].Created, Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1790000000123)));
            Assert.That(mine.GeneratedBy, Is.EqualTo(OkfActors.Self));
            Assert.That(mine.Verified, Is.EqualTo(new[] { new OkfVerification(OkfActors.Self, DateTimeOffset.FromUnixTimeMilliseconds(1790000000123)) }));
            Assert.That(observations.Single(o => o != mine).Verified, Is.Empty);
        });
    }

    [Test]
    public async Task The_old_notes_are_kept_aside()
    {
        await using var connection = await _parts.Connections.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT count(*) FROM contact_note_v1_backup) || ' ' || (SELECT count(*) FROM sqlite_master WHERE name = 'contact_note')";

        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo("2 0"));
    }
}
