using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Hands a finished transcript to tawk, which keeps it beside the voice note and shows it in the
/// conversation. tawk-mcp itself still writes no transcript to disk.
/// </summary>
public interface ITranscriptHandoff
{
    /// <summary>
    /// Returns whether tawk kept it. It does not when tawk is from before it kept transcripts, when the
    /// chat is switched off, or when tawk is down; none of these is an error for the caller.
    /// </summary>
    /// <param name="replace">Take the place of every transcript the voice note has, in whatever language: it was written out again.</param>
    Task<bool> HandOverAsync(string messageId, string language, Transcript transcript, bool replace, CancellationToken cancellationToken);
}
