namespace Tawk.Mcp.ResourceAccess.Media;

/// <summary>Reads files tawk named. Nothing is ever written, and a path never comes from a model.</summary>
public interface IMediaFiles
{
    /// <summary>Throws <see cref="Core.Media.MediaException"/> unless the path is a plain file of at most <paramref name="maxBytes"/>.</summary>
    void Check(string path, long maxBytes);

    Task<ReadOnlyMemory<byte>> ReadAsync(string path, long maxBytes, CancellationToken cancellationToken);
}
