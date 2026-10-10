namespace Tawk.Mcp.Core.Media;

/// <summary>A downloaded file as tawk named it. The path is never shown to a model.</summary>
public sealed record MediaFile(string MessageId, string Path, string? Type, ChatRef? Chat)
{
    /// <summary>False when tawk says the chat's voice notes are not to be transcribed.</summary>
    public bool Transcribe { get; init; } = true;
}
