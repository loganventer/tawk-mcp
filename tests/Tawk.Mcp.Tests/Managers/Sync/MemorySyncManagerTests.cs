using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers.Sync;

public class MemorySyncManagerTests
{
    private const string Anneke = "27820000001@s.whatsapp.net";
    private FakeMemoryRemote _remote = null!;
    private SyncedMachine _a = null!;
    private SyncedMachine _b = null!;

    [SetUp]
    public void SetUp()
    {
        _remote = new FakeMemoryRemote();
        _a = new SyncedMachine(_remote);
        _b = new SyncedMachine(_remote);
    }

    [TearDown]
    public void TearDown()
    {
        _a.Dispose();
        _b.Dispose();
    }

    [Test]
    public async Task Two_machines_end_up_with_each_others_notes_and_then_stop_pushing()
    {
        await _a.Parts.Profiles.AddNoteAsync("Anneke Venter", "From the laptop.", "user", default);
        _b.Parts.Clock.Advance(TimeSpan.FromMinutes(1));
        await _b.Parts.Profiles.AddNoteAsync("Anneke Venter", "From the desktop.", "user", default);
        await _b.Parts.Profiles.AddNoteAsync("Neal Titus", "Only on the desktop.", "inferred", default);

        var first = await _a.SyncAsync();
        var second = await _b.SyncAsync();
        var third = await _a.SyncAsync();
        var pushes = _remote.Pushes;
        var quiet = new[] { await _b.SyncAsync(), await _a.SyncAsync(), await _b.SyncAsync() };

        Assert.Multiple(async () =>
        {
            Assert.That((first, second, third), Is.EqualTo((SyncOutcome.Pushed, SyncOutcome.PulledAndPushed, SyncOutcome.Pulled)));
            Assert.That(quiet, Is.All.EqualTo(SyncOutcome.UpToDate));
            Assert.That(_remote.Pushes, Is.EqualTo(pushes).And.EqualTo(2));
            Assert.That(await NotesAsync(_a, "Anneke Venter"), Is.EquivalentTo(new[] { "From the laptop.", "From the desktop." }));
            Assert.That(await NotesAsync(_b, "Anneke Venter"), Is.EquivalentTo(new[] { "From the laptop.", "From the desktop." }));
            Assert.That(await NotesAsync(_a, "Neal Titus"), Is.EqualTo(new[] { "Only on the desktop." }));
            Assert.That(_remote.Messages, Is.EqualTo(new[] { "Add memory", "Sync memory (merged from v000000)" }));
        });
    }

    [Test]
    public async Task Nothing_is_downloaded_while_the_remote_stands_still()
    {
        await _a.Parts.Profiles.AddNoteAsync("Anneke Venter", "One.", "user", default);
        await _a.SyncAsync();
        await _b.SyncAsync();
        var downloads = _remote.Downloads;

        await _b.SyncAsync();
        await _b.Parts.Profiles.AddNoteAsync("Anneke Venter", "Two.", "user", default);
        var outcome = await _b.SyncAsync();

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(SyncOutcome.Pushed));
            Assert.That(_remote.Downloads, Is.EqualTo(downloads));
        });
    }

    [Test]
    public async Task Everything_memory_holds_crosses_over()
    {
        await _a.Parts.CategoryManager.SetAsync("family/spouse", "the closest", default);
        await _a.Parts.Profiles.SetFieldsAsync("Anneke Venter", """{"relation":"spouse"}""", "user", null, null, default);
        await _a.Parts.Profiles.SetCategoriesAsync("Anneke Venter", ["family/spouse"], null, default);
        await _a.Parts.KnowledgeManager.RecordRelationAsync("Anneke Venter", "spouse of", "self", "user", null, null, default);
        await _a.Parts.Templates.UpsertAsync(new ResponseTemplate("thanks", null, null, null, null, "Dankie!", _a.Parts.Clock.GetUtcNow()), default);

        await _a.SyncAsync();
        await _b.SyncAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await _b.Parts.Profiles.GetContactAsync("Anneke Venter", false, default),
                Does.Contain("Categories: family/spouse").And.Contain("relation: \"spouse\" (user)"));
            Assert.That(await _b.Parts.KnowledgeManager.GetKnowledgeAsync("self", false, default), Does.Contain("Anneke Venter is spouse of this"));
            Assert.That((await _b.Parts.Templates.GetAsync("thanks", default))!.Body, Is.EqualTo("Dankie!"));
            Assert.That((await _b.Parts.Categories.GetAsync("family/spouse", default))!.Description, Is.EqualTo("the closest"));
        });
    }

    [Test]
    public async Task A_delete_on_one_machine_reaches_the_other()
    {
        await _a.Parts.Profiles.SetFieldsAsync("Anneke Venter", """{"relation":"spouse","humour_ok":true}""", "user", null, null, default);
        await _a.Parts.Profiles.SetFieldsAsync("Neal Titus", """{"relation":"friend"}""", "user", null, null, default);
        await _a.Parts.Profiles.AddNoteAsync("Neal Titus", "A note.", "user", default);
        await _a.SyncAsync();
        await _b.SyncAsync();

        _b.Parts.Clock.Advance(TimeSpan.FromMinutes(5));
        await _b.Parts.Profiles.ForgetFieldAsync("Anneke Venter", "humour_ok", default);
        await _b.Parts.Contacts.DeleteAsync("27820000003@s.whatsapp.net", default);
        await _b.SyncAsync();
        var outcome = await _a.SyncAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(outcome, Is.EqualTo(SyncOutcome.Pulled));
            Assert.That((await _a.Parts.Contacts.GetFactsAsync(Anneke, default)).Select(f => f.Field), Is.EqualTo(new[] { "relation" }));
            Assert.That(await _a.Parts.Contacts.GetAsync("27820000003@s.whatsapp.net", default), Is.Null);
            Assert.That(await _a.Parts.Knowledge.ListAsync(OkfTypes.Observation, null, null, null, default), Is.Empty);
            Assert.That(await _b.SyncAsync(), Is.EqualTo(SyncOutcome.UpToDate));
        });
    }

    [Test]
    public async Task What_was_deleted_and_then_stated_again_comes_back()
    {
        await _a.Parts.Profiles.SetFieldsAsync("Anneke Venter", """{"humour_ok":true}""", "user", null, null, default);
        await _a.SyncAsync();
        await _b.SyncAsync();
        _b.Parts.Clock.Advance(TimeSpan.FromMinutes(5));
        await _b.Parts.Profiles.ForgetFieldAsync("Anneke Venter", "humour_ok", default);
        await _b.SyncAsync();

        _a.Parts.Clock.Advance(TimeSpan.FromMinutes(10));
        await _a.Parts.Profiles.SetFieldsAsync("Anneke Venter", """{"humour_ok":false}""", "user", null, null, default);
        await _a.SyncAsync();
        await _b.SyncAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((await _b.Parts.Contacts.GetFactsAsync(Anneke, default)).Single().ValueJson, Is.EqualTo("false"));
            Assert.That(await _a.SyncAsync(), Is.EqualTo(SyncOutcome.UpToDate));
            Assert.That(await _b.SyncAsync(), Is.EqualTo(SyncOutcome.UpToDate));
        });
    }

    [Test]
    public async Task What_the_user_stated_beats_a_later_inference_from_another_machine()
    {
        await _a.Parts.Profiles.SetFieldsAsync("Anneke Venter", """{"relation":"spouse"}""", "user", null, null, default);
        _b.Parts.Clock.Advance(TimeSpan.FromDays(1));
        await _b.Parts.Profiles.SetFieldsAsync("Anneke Venter", """{"relation":"colleague"}""", "inferred", 0.4, null, default);

        await _b.SyncAsync();
        await _a.SyncAsync();
        await _b.SyncAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((await _a.Parts.Contacts.GetFactsAsync(Anneke, default)).Single().ValueJson, Is.EqualTo("\"spouse\""));
            Assert.That((await _b.Parts.Contacts.GetFactsAsync(Anneke, default)).Single().ValueJson, Is.EqualTo("\"spouse\""));
        });
    }

    [Test]
    public async Task Only_one_voice_stays_the_default()
    {
        await _a.Parts.Voices.UpsertAsync(new Voice("plain", null, "guide", new VoiceRules(), true, _a.Parts.Clock.GetUtcNow()), default);
        _b.Parts.Clock.Advance(TimeSpan.FromMinutes(1));
        await _b.Parts.Voices.UpsertAsync(new Voice("warm", null, "guide", new VoiceRules(), true, _b.Parts.Clock.GetUtcNow()), default);

        await _a.SyncAsync();
        await _b.SyncAsync();
        await _a.SyncAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((await _a.Parts.Voices.ListAsync(default)).Where(v => v.IsDefault).Select(v => v.Name), Is.EqualTo(new[] { "warm" }));
            Assert.That((await _b.Parts.Voices.ListAsync(default)).Where(v => v.IsDefault).Select(v => v.Name), Is.EqualTo(new[] { "warm" }));
            Assert.That(await _b.SyncAsync(), Is.EqualTo(SyncOutcome.UpToDate));
        });
    }

    [Test]
    public async Task A_push_that_loses_the_race_is_tried_again()
    {
        await _a.Parts.Profiles.AddNoteAsync("Anneke Venter", "One.", "user", default);
        _remote.ConflictsToCome = 2;

        Assert.Multiple(async () =>
        {
            Assert.That(await _a.SyncAsync(), Is.EqualTo(SyncOutcome.Pushed));
            Assert.That(_remote.Pushes, Is.EqualTo(1));
        });

        await _a.Parts.Profiles.AddNoteAsync("Anneke Venter", "Two.", "user", default);
        _remote.ConflictsToCome = 3;
        Assert.That(await _a.SyncAsync(), Is.EqualTo(SyncOutcome.Failed));
    }

    [Test]
    public async Task Sync_is_off_without_a_token_and_waits_its_turn()
    {
        using var off = new SyncedMachine(_remote, token: null);
        await _a.Parts.Profiles.AddNoteAsync("Anneke Venter", "One.", "user", default);

        using (_a.Lock.TryAcquire())
        {
            Assert.That(await _a.SyncAsync(), Is.EqualTo(SyncOutcome.Busy));
        }

        Assert.Multiple(async () =>
        {
            Assert.That(await off.SyncAsync(), Is.EqualTo(SyncOutcome.Disabled));
            Assert.That(await _a.SyncAsync(), Is.EqualTo(SyncOutcome.Pushed));
            Assert.That(_remote.Pushes, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_remote_from_the_first_schema_is_merged()
    {
        _remote.Put(Database(
            """
            CREATE TABLE category (path TEXT PRIMARY KEY, description TEXT);
            CREATE TABLE voice (name TEXT PRIMARY KEY, description TEXT, guide TEXT NOT NULL, rules TEXT NOT NULL, is_default INTEGER NOT NULL DEFAULT 0, updated INTEGER NOT NULL);
            CREATE TABLE voice_variant (voice TEXT NOT NULL REFERENCES voice(name) ON DELETE CASCADE, category TEXT NOT NULL, guide TEXT NOT NULL, rules TEXT NOT NULL, examples TEXT NOT NULL, updated INTEGER NOT NULL, PRIMARY KEY (voice, category));
            CREATE TABLE contact (jid TEXT PRIMARY KEY, display_name TEXT, voice TEXT, updated INTEGER NOT NULL);
            CREATE TABLE contact_category (jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE, category TEXT NOT NULL, PRIMARY KEY (jid, category));
            CREATE TABLE contact_fact (jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE, field TEXT NOT NULL, value TEXT NOT NULL, source TEXT NOT NULL, confidence REAL NOT NULL, evidence TEXT, sensitive INTEGER NOT NULL, updated INTEGER NOT NULL, expires INTEGER, PRIMARY KEY (jid, field));
            CREATE TABLE contact_note (id INTEGER PRIMARY KEY AUTOINCREMENT, jid TEXT NOT NULL REFERENCES contact(jid) ON DELETE CASCADE, text TEXT NOT NULL, source TEXT NOT NULL, created INTEGER NOT NULL);
            CREATE TABLE response_template (name TEXT PRIMARY KEY, description TEXT, category TEXT, voice TEXT, language TEXT, body TEXT NOT NULL, updated INTEGER NOT NULL);
            INSERT INTO contact VALUES ('27820000001@s.whatsapp.net', 'Anneke Venter', NULL, 1790000000000);
            INSERT INTO contact_note (jid, text, source, created) VALUES ('27820000001@s.whatsapp.net', 'From before.', 'user', 1790000000123);
            PRAGMA user_version = 1;
            """));

        var outcome = await _a.SyncAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(outcome, Is.EqualTo(SyncOutcome.Pulled));
            Assert.That(await NotesAsync(_a, "Anneke Venter"), Is.EqualTo(new[] { "From before." }));
        });
    }

    [Test]
    public async Task A_remote_from_a_newer_tawk_mcp_or_one_that_is_no_database_is_left_alone()
    {
        await _a.Parts.Profiles.AddNoteAsync("Anneke Venter", "Mine.", "user", default);
        _remote.Put(Database("CREATE TABLE future (x); PRAGMA user_version = 99;"));
        var newer = await _a.Sync.SyncAsync(default);
        _remote.Put("not a database at all, just text that is longer than a header"u8.ToArray());
        var broken = await _a.Sync.SyncAsync(default);

        Assert.Multiple(() =>
        {
            Assert.That(newer.Outcome, Is.EqualTo(SyncOutcome.Failed));
            Assert.That(newer.Message, Does.Contain("newer tawk-mcp"));
            Assert.That(broken.Outcome, Is.EqualTo(SyncOutcome.Failed));
            Assert.That(_remote.Pushes, Is.Zero);
        });
    }

    private static async Task<List<string>> NotesAsync(SyncedMachine machine, string chat)
    {
        var jid = (await machine.Parts.Chats.ResolveAsync(chat, default)).Jid;
        return (await machine.Parts.Contacts.GetNotesAsync(jid, default)).Select(n => n.Text).ToList();
    }

    private static byte[] Database(string sql)
    {
        var path = Path.Combine(Path.GetTempPath(), "tawk-remote-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }

            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
