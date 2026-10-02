using System.Runtime.Versioning;
using System.Text.Json;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Engines.Knowledge;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers.Memory;

public class OkfBundleManagerTests
{
    private const string Anneke = "27820000001@s.whatsapp.net";
    private MemoryParts _parts = null!;
    private MemoryParts _other = null!;
    private string _folder = null!;

    [SetUp]
    public void SetUp()
    {
        _parts = new MemoryParts();
        _other = new MemoryParts();
        _folder = Path.Combine(_parts.Folder, "bundle");
    }

    [TearDown]
    public void TearDown()
    {
        _parts.Dispose();
        _other.Dispose();
    }

    [Test]
    public async Task An_exported_bundle_conforms_to_the_format()
    {
        await FillAsync();

        await _parts.Bundles.ExportAsync(_folder, false, default);
        var files = Directory.EnumerateFiles(_folder, "*.md", SearchOption.AllDirectories).ToList();
        var index = await File.ReadAllTextAsync(Path.Combine(_folder, "index.md"));
        var contact = await File.ReadAllTextAsync(Path.Combine(_folder, "contacts", Anneke + ".md"));

        Assert.Multiple(() =>
        {
            // Every concept document opens with frontmatter that has a type; only the root index may differ.
            foreach (var file in files.Where(f => Path.GetFileName(f) != "index.md"))
            {
                var text = File.ReadAllText(file);
                Assert.That(text, Does.StartWith("---\ntype: "), file);
                Assert.That(text.IndexOf("\n---\n", 4, StringComparison.Ordinal), Is.GreaterThan(0), file);
            }

            Assert.That(index, Does.StartWith("---\nokf_version: \"0.2\"\n---\n"));
            Assert.That(index, Does.Contain("# Contacts\n\n* [Anneke Venter](contacts/" + Anneke + ".md)"));
            Assert.That(index, Does.Contain("# Observations").And.Contain(" - Starts a new job in March."));
            Assert.That(contact, Does.Contain("title: Anneke Venter\nresource: \"whatsapp:" + Anneke + "\"\n"));
            Assert.That(contact, Does.Match(@"generated:\n  by: tawk-mcp/test\n  at: \d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z\n"));
            Assert.That(contact, Does.Contain("status: stable\n"));
            Assert.That(contact, Does.Contain("# Relations\n\n* spouse of [the user](/self.md): since 2010\n"));
            Assert.That(contact, Does.Contain("profile:\n  humour_ok:\n    value: true\n").And.Contain("  relation:\n    value: spouse\n    source: user\n"));
        });
    }

    [Test]
    public async Task A_bundle_imports_back_unchanged()
    {
        await FillAsync();
        await _parts.Bundles.ExportAsync(_folder, true, default);

        var result = await _other.Bundles.ImportAsync(_folder, default);
        var again = await _other.Bundles.ImportAsync(_folder, default);

        Assert.Multiple(async () =>
        {
            Assert.That(await ConceptsAsync(_other), Is.EqualTo(await ConceptsAsync(_parts)));
            Assert.That(await _other.Knowledge.ListLinksAsync(default), Is.EqualTo(await _parts.Knowledge.ListLinksAsync(default)));
            Assert.That(await _other.Contacts.GetFactsAsync(Anneke, default), Is.EqualTo(await _parts.Contacts.GetFactsAsync(Anneke, default)));
            Assert.That(result, Does.Contain("took 6, 4 and 3."));
            Assert.That(again, Does.Contain("took 0, 0 and 0."));
        });
    }

    [Test]
    public async Task Sensitive_knowledge_is_left_out_unless_asked_for()
    {
        await FillAsync();

        var result = await _parts.Bundles.ExportAsync(_folder, false, default);
        var all = string.Concat(Directory.EnumerateFiles(_folder, "*.md", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Sensitive ones were left out."));
            Assert.That(all, Does.Not.Contain("unwell").And.Not.Contain("sensitive_notes"));
            Assert.That(all, Does.Contain("Starts a new job in March."));
        });
    }

    [Test]
    public async Task A_bundle_from_elsewhere_is_read_as_the_format_allows()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "tables"));
        await File.WriteAllTextAsync(Path.Combine(_folder, "index.md"), "# Tables\n\n* [Orders](tables/orders.md) - orders\n");
        await File.WriteAllTextAsync(Path.Combine(_folder, "tables", "orders.md"),
            "---\r\ntype: BigQuery Table\r\ntitle: Orders\r\ntags: [sales, core]\r\n"
            + "verified: { by: human:ahormati, at: 2026-06-25T09:00:00Z }\r\nowner: team:sales\r\n---\r\n"
            + "# Schema\r\n\r\nJoins [customers](./customers.md) and the [playbook](/playbooks/refunds.md#steps). See [docs](https://example.com/a.md).\r\n");
        await File.WriteAllTextAsync(Path.Combine(_folder, "tables", "customers.md"), "---\ntype: BigQuery Table\n---\n");
        await File.WriteAllTextAsync(Path.Combine(_folder, "tables", "broken.md"), "no frontmatter here\n");
        await File.WriteAllTextAsync(Path.Combine(_folder, "tables", "untyped.md"), "---\ntitle: nothing\n---\n");

        var result = await _parts.Bundles.ImportAsync(_folder, default);
        var orders = await _parts.Knowledge.GetAsync("tables/orders", default);
        var links = await _parts.Knowledge.GetLinksFromAsync("tables/orders", default);

        Assert.Multiple(() =>
        {
            Assert.That(orders!.Type, Is.EqualTo("BigQuery Table"));
            Assert.That(orders.Tags, Is.EqualTo(new[] { "sales", "core" }));
            Assert.That(orders.GeneratedBy, Is.EqualTo(OkfActors.Import));
            Assert.That(orders.Verified, Is.EqualTo(new[] { new OkfVerification("human:ahormati", new DateTimeOffset(2026, 6, 25, 9, 0, 0, TimeSpan.Zero)) }));
            Assert.That(orders.ExtraJson, Is.EqualTo("""{"owner":"team:sales"}"""));
            Assert.That(orders.Body, Does.StartWith("# Schema"));
            Assert.That(links.Select(l => (l.ToId, l.Label, l.Source)), Is.EquivalentTo(new[]
            {
                ("tables/customers", "related to", FactSource.Imported), ("playbooks/refunds", "related to", FactSource.Imported),
            }));
            Assert.That(result, Does.Contain("Read 2 concept(s)").And.Contain("2 file(s) were not read").And.Contain("broken.md").And.Contain("untyped.md"));
        });
    }

    [Test]
    public async Task An_import_never_replaces_what_is_stronger_or_newer()
    {
        await _parts.KnowledgeManager.RecordRelationAsync("Anneke Venter", "friend of", "self", "inferred", 0.4, "a guess", default);
        await _parts.Bundles.ExportAsync(_folder, true, default);
        await _other.KnowledgeManager.RecordRelationAsync("Anneke Venter", "friend of", "self", "user", null, "stated", default);

        await _other.Bundles.ImportAsync(_folder, default);

        Assert.That((await _other.Knowledge.GetLinksFromAsync(OkfIds.Contact(Anneke), default)).Single().Note, Is.EqualTo("stated"));
    }

    [Test]
    public async Task Text_that_yaml_could_misread_survives()
    {
        var concept = new OkfConcept(
            "concepts/odd", "Concept", "yes: \"quoted\" #1 😀\nsecond line", "007", null, ["on", "a b"], "tawk-mcp/test", _parts.Clock.GetUtcNow(), [],
            OkfStatus.Draft, null, [], """{"nested":{"list":[1,{"k":"v: w"}],"n":null},"flag":false}""", "Body with --- inside\n---\nand more", _parts.Clock.GetUtcNow());
        await _parts.Knowledge.UpsertAsync(concept, default);

        await _parts.Bundles.ExportAsync(_folder, true, default);
        await _other.Bundles.ImportAsync(_folder, default);

        Assert.That(
            JsonSerializer.Serialize(await _other.Knowledge.GetAsync("concepts/odd", default)),
            Is.EqualTo(JsonSerializer.Serialize(await _parts.Knowledge.GetAsync("concepts/odd", default))));
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public async Task The_bundle_is_private_and_never_written_over_other_files()
    {
        await FillAsync();
        await _parts.Bundles.ExportAsync(_folder, false, default);

        Assert.Multiple(() =>
        {
            Assert.That(File.GetUnixFileMode(Path.Combine(_folder, "index.md")), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
            Assert.That(File.GetUnixFileMode(_folder), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
            Assert.ThrowsAsync<MemoryException>(() => _parts.Bundles.ExportAsync(_folder, false, default));
            Assert.ThrowsAsync<MemoryException>(() => _parts.Bundles.ImportAsync(Path.Combine(_folder, "missing"), default));
        });
    }

    [Test]
    public void The_stronger_source_wins_then_the_newer_one()
    {
        var precedence = new KnowledgePrecedence();
        var now = _parts.Clock.GetUtcNow();

        Assert.Multiple(() =>
        {
            Assert.That(precedence.Replaces(FactSource.User, now, FactSource.Inferred, now.AddDays(1)), Is.True);
            Assert.That(precedence.Replaces(FactSource.Inferred, now.AddDays(1), FactSource.Contact, now), Is.False);
            Assert.That(precedence.Replaces(FactSource.Contact, now.AddSeconds(1), FactSource.Contact, now), Is.True);
            Assert.That(precedence.Replaces(FactSource.Contact, now, FactSource.Contact, now), Is.False);
        });
    }

    // Records holding lists compare by reference, so concepts are compared as JSON.
    private static async Task<string> ConceptsAsync(MemoryParts parts) =>
        JsonSerializer.Serialize((await parts.Knowledge.ListAsync(null, null, null, null, default)).OrderBy(c => c.Id, StringComparer.Ordinal));

    private async Task FillAsync()
    {
        await _parts.Profiles.SetFieldsAsync("Anneke Venter", """{"relation":"spouse","sensitive_notes":"asthma"}""", "user", null, null, default);
        await _parts.Profiles.SetFieldsAsync("Anneke Venter", """{"humour_ok":true}""", "inferred", 0.6, "m1", default);
        await _parts.KnowledgeManager.RecordObservationAsync("Anneke Venter", "Starts a new job in March.", "user", ["work"], null, false, null, null, default);
        await _parts.KnowledgeManager.RecordObservationAsync("Anneke Venter", "Has been unwell.", "user", ["health"], null, true, null, null, default);
        await _parts.KnowledgeManager.RecordObservationAsync("concepts/cape-town-trip", "Flights are booked.", "inferred", null, 0.7, false, "m2", 30, default);
        await _parts.KnowledgeManager.RecordRelationAsync("Anneke Venter", "spouse of", "self", "user", null, "since 2010", default);
    }
}
