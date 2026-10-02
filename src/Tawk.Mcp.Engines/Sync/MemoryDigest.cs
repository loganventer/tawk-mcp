using System.Security.Cryptography;
using System.Text;
using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.Engines.Sync;

public sealed class MemoryDigest : IMemoryDigest
{
    public string Compute(MemorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (table, rows) in snapshot.Tables.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            Add(hash, "T" + table);
            foreach (var row in rows.OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                Add(hash, SyncValues.Write(row.Values));
            }
        }

        foreach (var tombstone in snapshot.Tombstones.OrderBy(t => t.Kind, StringComparer.Ordinal).ThenBy(t => t.Key, StringComparer.Ordinal))
        {
            Add(hash, SyncValues.Write(["D", tombstone.Kind, tombstone.Key, tombstone.Deleted]));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Add(IncrementalHash hash, string text) => hash.AppendData(Encoding.UTF8.GetBytes(text + "\n"));
}
