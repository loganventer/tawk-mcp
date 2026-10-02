using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Engines.Sync;

namespace Tawk.Mcp.Tests.Engines.Sync;

public class MemoryMergerTests
{
    private const string Table = "contact_fact";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1000);
    private readonly MemoryMerger _merger = new();

    [Test]
    public void A_row_only_the_remote_has_is_taken_and_one_only_here_is_left_alone()
    {
        var plan = _merger.Merge(Snapshot([Row("mine", FactSource.User, 10, "a")]), Snapshot([Row("theirs", FactSource.Inferred, 10, "b")]), Now);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Upserts.Select(u => u.Row.Key), Is.EqualTo(new[] { "theirs" }));
            Assert.That(plan.Deletes, Is.Empty);
        });
    }

    [Test]
    public void The_stronger_source_wins_whatever_the_dates()
    {
        var stated = Row("k", FactSource.User, 10, "stated");
        var guessed = Row("k", FactSource.Inferred, 99, "guessed");

        Assert.Multiple(() =>
        {
            Assert.That(_merger.Merge(Snapshot([stated]), Snapshot([guessed]), Now).IsEmpty, Is.True);
            Assert.That(_merger.Merge(Snapshot([guessed]), Snapshot([stated]), Now).Upserts.Single().Row, Is.SameAs(stated));
        });
    }

    [Test]
    public void Between_equal_sources_the_newer_change_wins()
    {
        var older = Row("k", FactSource.Contact, 10, "old");
        var newer = Row("k", FactSource.Contact, 11, "new");

        Assert.Multiple(() =>
        {
            Assert.That(_merger.Merge(Snapshot([older]), Snapshot([newer]), Now).Upserts.Single().Row, Is.SameAs(newer));
            Assert.That(_merger.Merge(Snapshot([newer]), Snapshot([older]), Now).IsEmpty, Is.True);
        });
    }

    [Test]
    public void An_exact_tie_goes_the_same_way_on_both_machines()
    {
        var a = Row("k", FactSource.User, 10, "apple");
        var b = Row("k", FactSource.User, 10, "berry");

        var onA = _merger.Merge(Snapshot([a]), Snapshot([b]), Now);
        var onB = _merger.Merge(Snapshot([b]), Snapshot([a]), Now);

        Assert.Multiple(() =>
        {
            Assert.That(onA.Upserts.Single().Row, Is.SameAs(b));
            Assert.That(onB.IsEmpty, Is.True);
        });
    }

    [Test]
    public void A_delete_newer_than_the_row_removes_it_and_is_remembered()
    {
        var gone = new SyncTombstone(Table, "k", 20);

        var plan = _merger.Merge(Snapshot([Row("k", FactSource.User, 10, "x")]), Snapshot([], gone), Now);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Deletes, Is.EqualTo(new[] { new SyncRowKey(Table, "k") }));
            Assert.That(plan.Tombstones, Is.EqualTo(new[] { gone }));
            Assert.That(plan.Upserts, Is.Empty);
        });
    }

    [Test]
    public void A_local_delete_keeps_the_remote_row_out()
    {
        var plan = _merger.Merge(Snapshot([], new SyncTombstone(Table, "k", 20)), Snapshot([Row("k", FactSource.User, 10, "x")]), Now);

        Assert.That(plan.IsEmpty, Is.True);
    }

    [Test]
    public void A_row_changed_after_its_delete_survives_and_clears_the_delete()
    {
        var back = Row("k", FactSource.User, 30, "back");

        var plan = _merger.Merge(Snapshot([], new SyncTombstone(Table, "k", 20)), Snapshot([back]), Now);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Upserts.Single().Row, Is.SameAs(back));
            Assert.That(plan.ClearedTombstones, Is.EqualTo(new[] { new SyncRowKey(Table, "k") }));
            Assert.That(plan.Deletes, Is.Empty);
        });
    }

    [Test]
    public void A_delete_of_a_row_neither_side_has_is_still_passed_on()
    {
        var gone = new SyncTombstone(Table, "k", 20);

        Assert.Multiple(() =>
        {
            Assert.That(_merger.Merge(Snapshot([]), Snapshot([], gone), Now).Tombstones, Is.EqualTo(new[] { gone }));
            Assert.That(_merger.Merge(Snapshot([], gone), Snapshot([], gone), Now).IsEmpty, Is.True);
            Assert.That(_merger.Merge(Snapshot([], new SyncTombstone(Table, "k", 25)), Snapshot([], gone), Now).IsEmpty, Is.True);
        });
    }

    [Test]
    public void What_has_lapsed_on_the_remote_is_not_brought_back()
    {
        var lapsed = new SyncRow("k", FactSource.Inferred, 10, 999, ["k", "x"]);

        Assert.That(_merger.Merge(Snapshot([]), Snapshot([lapsed]), Now).IsEmpty, Is.True);
    }

    [Test]
    public void The_digest_follows_the_content_and_not_its_order()
    {
        var digest = new MemoryDigest();
        var a = Row("a", FactSource.User, 1, "x");
        var b = Row("b", FactSource.User, 2, "y");

        Assert.Multiple(() =>
        {
            Assert.That(digest.Compute(Snapshot([a, b])), Is.EqualTo(digest.Compute(Snapshot([b, a]))));
            Assert.That(digest.Compute(Snapshot([a, b])), Is.Not.EqualTo(digest.Compute(Snapshot([a]))));
            Assert.That(digest.Compute(Snapshot([a])), Is.Not.EqualTo(digest.Compute(Snapshot([a], new SyncTombstone(Table, "z", 1)))));
        });
    }

    private static SyncRow Row(string key, FactSource source, long updated, string value) => new(key, source, updated, null, [key, value, updated]);

    private static MemorySnapshot Snapshot(SyncRow[] rows, params SyncTombstone[] gone) =>
        new(new Dictionary<string, IReadOnlyList<SyncRow>> { [Table] = rows }, gone);
}
