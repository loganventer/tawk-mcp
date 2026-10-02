using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.Engines.Sync;

public sealed class MemoryMerger : IMemoryMerger
{
    public MergePlan Merge(MemorySnapshot local, MemorySnapshot remote, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);
        var upserts = new List<SyncRowChange>();
        var deletes = new List<SyncRowKey>();
        var tombstones = new List<SyncTombstone>();
        var cleared = new List<SyncRowKey>();
        var localGone = local.Tombstones.ToDictionary(t => new SyncRowKey(t.Kind, t.Key));
        var remoteGone = remote.Tombstones.ToDictionary(t => new SyncRowKey(t.Kind, t.Key));
        var nowMs = now.ToUnixTimeMilliseconds();
        var seen = new HashSet<SyncRowKey>();

        foreach (var table in local.Tables.Keys.Union(remote.Tables.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var mine = Rows(local, table).ToDictionary(r => r.Key, StringComparer.Ordinal);

            // What has lapsed on the remote is not brought back: each machine drops lapsed rows by itself.
            var theirs = Rows(remote, table).Where(r => !(r.Expires <= nowMs)).ToDictionary(r => r.Key, StringComparer.Ordinal);
            foreach (var key in mine.Keys.Union(theirs.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var id = new SyncRowKey(table, key);
                seen.Add(id);
                mine.TryGetValue(key, out var here);
                theirs.TryGetValue(key, out var there);
                var winner = Winner(here, there);
                localGone.TryGetValue(id, out var goneHere);
                var gone = Newer(goneHere, remoteGone.GetValueOrDefault(id));
                if (gone is not null && gone.Deleted >= winner.Updated)
                {
                    if (here is not null)
                    {
                        deletes.Add(id);
                    }

                    if (goneHere is null || goneHere.Deleted < gone.Deleted)
                    {
                        tombstones.Add(gone);
                    }

                    continue;
                }

                if (!ReferenceEquals(winner, here))
                {
                    upserts.Add(new SyncRowChange(table, winner));
                }

                if (goneHere is not null)
                {
                    cleared.Add(id);
                }
            }
        }

        // Deletes of rows neither side still has are kept, newest first, so a third machine hears of them.
        foreach (var (id, gone) in remoteGone)
        {
            if (!seen.Contains(id) && (!localGone.TryGetValue(id, out var goneHere) || goneHere.Deleted < gone.Deleted))
            {
                tombstones.Add(gone);
            }
        }

        return new MergePlan(upserts, deletes, tombstones, cleared);
    }

    private static IReadOnlyList<SyncRow> Rows(MemorySnapshot snapshot, string table) =>
        snapshot.Tables.TryGetValue(table, out var rows) ? rows : [];

    private static SyncTombstone? Newer(SyncTombstone? a, SyncTombstone? b) =>
        a is null ? b : b is null || a.Deleted >= b.Deleted ? a : b;

    // The same pair gives the same winner on either machine, so two machines never hand a row back and forth.
    private static SyncRow Winner(SyncRow? here, SyncRow? there)
    {
        if (here is null || there is null)
        {
            return here ?? there!;
        }

        var rank = (here.Source ?? FactSource.Inferred).CompareTo(there.Source ?? FactSource.Inferred);
        if (rank != 0)
        {
            return rank > 0 ? here : there;
        }

        if (here.Updated != there.Updated)
        {
            return here.Updated > there.Updated ? here : there;
        }

        return string.CompareOrdinal(SyncValues.Write(there.Values), SyncValues.Write(here.Values)) > 0 ? there : here;
    }
}
