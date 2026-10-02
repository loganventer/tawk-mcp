using System.Text;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.ResourceAccess.Memory;

public sealed class DiskOkfBundleFiles : IOkfBundleFiles
{
    private const long MaxFileBytes = 1024 * 1024;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateFolder = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly UTF8Encoding Utf8 = new(false);

    public async Task<IReadOnlyList<OkfBundleFile>> ReadAsync(string directory, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            throw new MemoryException($"There is no folder at {root}.");
        }

        var files = new List<OkfBundleFile>();
        foreach (var path in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (new FileInfo(path).Length > MaxFileBytes)
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            files.Add(new OkfBundleFile(relative, await File.ReadAllTextAsync(path, Utf8, cancellationToken).ConfigureAwait(false)));
        }

        return files;
    }

    public async Task WriteAsync(string directory, IReadOnlyList<OkfBundleFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        var root = Path.GetFullPath(directory);
        if (File.Exists(root) || (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()))
        {
            // Writing over a folder would leave files of concepts that no longer exist, or damage something unrelated.
            throw new MemoryException($"{root} is not empty. Export into a new or empty folder.");
        }

        CreateFolder(root);
        foreach (var file in files)
        {
            var path = Path.GetFullPath(Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new MemoryException($"{file.Path} would be written outside the bundle.");
            }

            CreateFolder(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.Content, Utf8, cancellationToken).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, PrivateFile);
            }
        }
    }

    private static void CreateFolder(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, PrivateFolder);
        }
    }
}
