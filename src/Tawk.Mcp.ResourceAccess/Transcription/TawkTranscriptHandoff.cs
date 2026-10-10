using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

public sealed class TawkTranscriptHandoff(ITawkControl control) : ITranscriptHandoff
{
    public async Task<bool> HandOverAsync(string messageId, string language, Transcript transcript, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        ArgumentNullException.ThrowIfNull(transcript);
        if (string.IsNullOrWhiteSpace(transcript.Text))
        {
            return false;
        }

        try
        {
            var hello = await control.ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!hello.Has(TawkFeatures.Transcripts))
            {
                return false;           // an older tawk: it has nowhere to keep one
            }

            var args = new JsonObject
            {
                ["message_id"] = messageId,
                ["language"] = Spoken(language, transcript),
                ["text"] = transcript.Text,
                ["model"] = transcript.Model ?? string.Empty,
            };
            await control.RequestAsync("set_transcript", args, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TawkControlException)
        {
            return false;               // switched off for the chat, the message is gone, or tawk is down
        }
    }

    // The language asked for, or the one the engine heard when it was left to detect.
    private static string Spoken(string language, Transcript transcript) =>
        string.IsNullOrWhiteSpace(language) || string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase)
            ? transcript.DetectedLanguage ?? string.Empty
            : language;
}
