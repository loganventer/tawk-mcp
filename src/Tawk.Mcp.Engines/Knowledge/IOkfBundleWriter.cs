using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>Writes knowledge as an Open Knowledge Format 0.2 bundle: one Markdown file per concept and an index.</summary>
public interface IOkfBundleWriter
{
    OkfBundleWriting Write(OkfBundle bundle);
}
