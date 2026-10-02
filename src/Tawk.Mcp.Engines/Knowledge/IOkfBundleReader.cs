using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>Reads an Open Knowledge Format bundle, written by tawk-mcp or by anything else that follows the format.</summary>
public interface IOkfBundleReader
{
    /// <summary><paramref name="now"/> dates whatever the files do not date themselves.</summary>
    OkfBundleReading Read(IReadOnlyList<OkfBundleFile> files, DateTimeOffset now);
}
