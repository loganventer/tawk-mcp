using Tawk.Mcp.Core.Media;
using Tawk.Mcp.Engines.Media;
using Tawk.Mcp.ResourceAccess.Media;

namespace Tawk.Mcp.Managers.Media;

public sealed class MediaViewingManager(
    ITawkMediaSource media, IMediaFiles files, IMediaTypeSniffer sniffer, MediaOptions options) : IMediaViewingManager
{
    public const string StillDownloading =
        "tawk is still downloading that file. Call again in a few seconds. "
        + "If this keeps happening, tawk may be too old to say when a download ends: update tawk.";

    public async Task<ImageData> ViewImageAsync(string messageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new MediaException("A message id is needed.");
        }

        var file = await media.LocateAsync(messageId.Trim(), options.DownloadWait, cancellationToken).ConfigureAwait(false)
            ?? throw new MediaException(StillDownloading);
        if (file.Type is { Length: > 0 } type && type is not ("image" or "sticker"))
        {
            throw new MediaException($"That message is {type}, not a picture.");
        }

        var bytes = await files.ReadAsync(file.Path, options.MaxImageBytes, cancellationToken).ConfigureAwait(false);
        var mime = sniffer.ImageType(bytes.Span)
            ?? throw new MediaException("That file is not a JPEG, PNG, GIF or WebP picture, so it cannot be shown.");
        return new ImageData(bytes, mime);
    }
}
