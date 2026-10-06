using Tawk.Mcp.Core.Media;
using Tawk.Mcp.ResourceAccess.Media;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Names a file for each message id it was given, and none for the rest.</summary>
public sealed class FakeMediaSource : ITawkMediaSource
{
    public Dictionary<string, MediaFile> Files { get; } = new(StringComparer.Ordinal);

    public List<string> Asked { get; } = [];

    public MediaException? Failure { get; set; }

    public Task<MediaFile?> LocateAsync(string messageId, TimeSpan wait, CancellationToken cancellationToken)
    {
        Asked.Add(messageId);
        return Failure is null ? Task.FromResult(Files.GetValueOrDefault(messageId)) : Task.FromException<MediaFile?>(Failure);
    }
}
