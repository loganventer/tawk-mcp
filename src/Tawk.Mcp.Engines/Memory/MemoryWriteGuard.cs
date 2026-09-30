using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

public sealed class MemoryWriteGuard(MemoryMode mode) : IMemoryWriteGuard
{
    public void EnsureWritable()
    {
        if (mode != MemoryMode.Write)
        {
            throw new MemoryException("tawk-mcp's memory is read-only (--memory read), so nothing was changed.");
        }
    }
}
