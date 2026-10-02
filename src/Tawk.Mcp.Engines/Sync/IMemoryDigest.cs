using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.Engines.Sync;

/// <summary>A fingerprint of what a memory database holds, the same for the same rows whatever the file's bytes.</summary>
public interface IMemoryDigest
{
    string Compute(MemorySnapshot snapshot);
}
