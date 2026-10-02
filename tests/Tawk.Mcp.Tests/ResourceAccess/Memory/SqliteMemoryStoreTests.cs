using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.ResourceAccess.Memory;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Memory;

public class SqliteMemoryStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero);
    private MemoryParts _parts = null!;

    [SetUp]
    public void SetUp() => _parts = new MemoryParts();

    [TearDown]
    public void TearDown() => _parts.Dispose();

    [Test]
    public void Nothing_is_created_until_memory_is_used()
    {
        Assert.That(File.Exists(_parts.Connections.Path), Is.False);
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public async Task The_database_is_private_to_the_user()
    {
        await _parts.Categories.ListAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(File.GetUnixFileMode(_parts.Connections.Path), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
            Assert.That(
                File.GetUnixFileMode(Path.GetDirectoryName(_parts.Connections.Path)!),
                Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
        });
    }

    [Test]
    public async Task Migrating_again_changes_nothing()
    {
        await _parts.Categories.UpsertAsync(new AudienceCategory("family", "kin"), CancellationToken.None);
        using var again = new SqliteConnectionFactory(_parts.Connections.Path, new SqliteSchemaMigrator());
        await using var connection = await again.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";

        Assert.Multiple(async () =>
        {
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(SqliteSchemaMigrator.CurrentVersion));
            Assert.That(await new SqliteCategoryStore(again, _parts.Clock).ListAsync(CancellationToken.None), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Voices_and_variants_round_trip_and_one_default_wins()
    {
        var rules = new VoiceRules { Case = "lower", ForbiddenPatterns = ["—"], Baseline = new StyleBaseline(12, 8, 3, 0.9, 0.2, 0.3, 0.2) };
        await _parts.Voices.UpsertAsync(new Voice("logan", "me", "guide", rules, true, Now), CancellationToken.None);
        await _parts.Voices.UpsertAsync(new Voice("work", null, "formal", VoiceRules.None, true, Now), CancellationToken.None);
        await _parts.Voices.UpsertVariantAsync(new VoiceVariant("logan", "family", "fam", rules, ["hey liefie"], Now), CancellationToken.None);

        var logan = await _parts.Voices.GetAsync("logan", CancellationToken.None);
        var variant = (await _parts.Voices.ListVariantsAsync("logan", CancellationToken.None)).Single();

        Assert.Multiple(async () =>
        {
            Assert.That(logan!.Rules, Is.EqualTo(rules).Using<VoiceRules>((a, b) => a.Case == b.Case && a.Baseline == b.Baseline && a.ForbiddenPatterns!.SequenceEqual(b.ForbiddenPatterns!)));
            Assert.That(logan.IsDefault, Is.False);
            Assert.That((await _parts.Voices.GetDefaultAsync(CancellationToken.None))!.Name, Is.EqualTo("work"));
            Assert.That(variant.Examples, Is.EqualTo(new[] { "hey liefie" }));
            Assert.That(await _parts.Voices.DeleteAsync("logan", CancellationToken.None), Is.True);
            Assert.That(await _parts.Voices.ListVariantsAsync("logan", CancellationToken.None), Is.Empty, "variants go with their voice");
        });
    }

    [Test]
    public async Task Deleting_a_contact_takes_its_facts_notes_and_categories_with_it()
    {
        const string jid = "27820000001@s.whatsapp.net";
        await _parts.Contacts.UpsertAsync(new ContactRecord(jid, "Anneke", null, Now), CancellationToken.None);
        await _parts.Contacts.SetCategoriesAsync(jid, ["family/spouse", "family/spouse"], CancellationToken.None);
        await _parts.Contacts.UpsertFactAsync(new ContactFact(jid, "relation", "\"spouse\"", FactSource.User, 1, null, false, Now, null), CancellationToken.None);
        var noteId = await _parts.Contacts.AddNoteAsync(new ContactNote(0, jid, "loves koeksisters", FactSource.User, Now), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(noteId, Is.GreaterThan(0));
            Assert.That(await _parts.Contacts.GetCategoriesAsync(jid, CancellationToken.None), Is.EqualTo(new[] { "family/spouse" }));
            Assert.That(await _parts.Contacts.ListAsync("family", null, CancellationToken.None), Has.Count.EqualTo(1), "a parent category finds children");
            Assert.That(await _parts.Contacts.ListAsync("fam", null, CancellationToken.None), Is.Empty);
            Assert.That(await _parts.Contacts.ListAsync(null, "anne", CancellationToken.None), Has.Count.EqualTo(1));
            Assert.That(await _parts.Contacts.DeleteAsync(jid, CancellationToken.None), Is.True);
            Assert.That(await _parts.Contacts.GetFactsAsync(jid, CancellationToken.None), Is.Empty);
            Assert.That(await _parts.Contacts.GetNotesAsync(jid, CancellationToken.None), Is.Empty);
            Assert.That(await _parts.Contacts.GetCategoriesAsync(jid, CancellationToken.None), Is.Empty);
        });
    }

    [Test]
    public async Task Expired_facts_are_purged()
    {
        const string jid = "27820000001@s.whatsapp.net";
        await _parts.Contacts.UpsertAsync(new ContactRecord(jid, "Anneke", null, Now), CancellationToken.None);
        await _parts.Contacts.UpsertFactAsync(new ContactFact(jid, "current_situation", "\"busy\"", FactSource.User, 1, null, false, Now, Now.AddDays(30)), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(await _parts.Contacts.PurgeExpiredAsync(Now.AddDays(29), CancellationToken.None), Is.Zero);
            Assert.That(await _parts.Contacts.PurgeExpiredAsync(Now.AddDays(30), CancellationToken.None), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Deleting_a_category_clears_it_everywhere()
    {
        await _parts.Categories.UpsertAsync(new AudienceCategory("elders", null), CancellationToken.None);
        await _parts.Voices.UpsertAsync(new Voice("logan", null, "g", VoiceRules.None, true, Now), CancellationToken.None);
        await _parts.Voices.UpsertVariantAsync(new VoiceVariant("logan", "elders", "oom", VoiceRules.None, [], Now), CancellationToken.None);
        await _parts.Templates.UpsertAsync(new ResponseTemplate("thanks", null, "elders", null, "af", "Dankie oom!", Now), CancellationToken.None);

        await _parts.Categories.DeleteAsync("elders", CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(await _parts.Voices.ListVariantsAsync("logan", CancellationToken.None), Is.Empty);
            Assert.That((await _parts.Templates.GetAsync("thanks", CancellationToken.None))!.Category, Is.Null);
        });
    }

    [Test]
    public async Task Template_lists_include_child_categories()
    {
        await _parts.Templates.UpsertAsync(new ResponseTemplate("a", null, "family/spouse", null, null, "x", Now), CancellationToken.None);
        await _parts.Templates.UpsertAsync(new ResponseTemplate("b", null, "work", null, null, "y", Now), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That((await _parts.Templates.ListAsync("family", CancellationToken.None)).Select(t => t.Name), Is.EqualTo(new[] { "a" }));
            Assert.That(await _parts.Templates.ListAsync(null, CancellationToken.None), Has.Count.EqualTo(2));
        });
    }
}
