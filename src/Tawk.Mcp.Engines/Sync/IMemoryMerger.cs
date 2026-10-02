using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.Engines.Sync;

/// <summary>Holds the rules for merging the remote memory into the local one.</summary>
public interface IMemoryMerger
{
    /// <summary>
    /// Rows are matched by key. The stronger source wins, then the newer change. A delete newer than the
    /// winning row removes it; a row changed after its delete survives. Two machines that merge each
    /// other's content reach the same result, whichever goes first.
    /// </summary>
    MergePlan Merge(MemorySnapshot local, MemorySnapshot remote, DateTimeOffset now);
}
