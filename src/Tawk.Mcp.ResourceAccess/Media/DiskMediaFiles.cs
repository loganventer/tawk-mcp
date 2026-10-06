using System.Globalization;
using Tawk.Mcp.Core.Media;

namespace Tawk.Mcp.ResourceAccess.Media;

public sealed class DiskMediaFiles : IMediaFiles
{
    public const string Missing =
        "tawk has the file, and tawk-mcp cannot read it where tawk keeps it. "
        + "In Docker, mount tawk's media folder read-only at the same path.";

    public void Check(string path, long maxBytes)
    {
        if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path))
        {
            throw new MediaException("tawk did not name a usable file.");
        }

        FileInfo file;
        try
        {
            file = new FileInfo(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            throw new MediaException("tawk did not name a usable file.", ex);
        }

        // A link could point anywhere, so only a plain file is read.
        if (!file.Exists || file.LinkTarget is not null || (file.Attributes & FileAttributes.Directory) != 0)
        {
            throw new MediaException(Missing);
        }

        if (file.Length == 0)
        {
            throw new MediaException("The file is empty.");
        }

        if (file.Length > maxBytes)
        {
            throw new MediaException(string.Create(
                CultureInfo.InvariantCulture, $"The file is {file.Length / 1024} KB, above the limit of {maxBytes / 1024} KB."));
        }
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string path, long maxBytes, CancellationToken cancellationToken)
    {
        Check(path, maxBytes);
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new MediaException(Missing, ex);
        }
    }
}
