namespace Tawk.Mcp.Core.Media;

/// <summary>Limits on the files tawk-mcp reads from tawk's media folder.</summary>
public sealed record MediaOptions
{
    public const int DefaultMaxImageBytes = 5 * 1024 * 1024;

    public const int DefaultMaxAudioBytes = 50 * 1024 * 1024;

    public int MaxImageBytes { get; init; } = DefaultMaxImageBytes;

    public int MaxAudioBytes { get; init; } = DefaultMaxAudioBytes;

    /// <summary>How long a call waits for tawk to finish a download before it says to try again.</summary>
    public TimeSpan DownloadWait { get; init; } = TimeSpan.FromSeconds(8);
}
