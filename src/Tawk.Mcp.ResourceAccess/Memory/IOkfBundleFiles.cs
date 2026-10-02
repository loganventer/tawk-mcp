using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>A bundle on disk: a folder of Markdown files.</summary>
public interface IOkfBundleFiles
{
    /// <summary>Every Markdown file under the folder, with paths from its root.</summary>
    Task<IReadOnlyList<OkfBundleFile>> ReadAsync(string directory, CancellationToken cancellationToken);

    /// <summary>Writes the files into a new or empty folder, readable only by the user.</summary>
    Task WriteAsync(string directory, IReadOnlyList<OkfBundleFile> files, CancellationToken cancellationToken);
}
