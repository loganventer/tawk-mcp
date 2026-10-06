using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Uses the transcriber the user runs while it answers, and the model inside tawk-mcp when it does not.
/// A circuit breaker decides: failures open it, and after a cooldown one pass tries the user's transcriber
/// again. When that works, tawk-mcp lets its own model go, so only one model is ever running.
/// </summary>
public sealed class FailoverTranscriber(
    ITranscriber primary, ITranscriber fallback, ICircuitBreaker breaker, IWhisperModelHost host) : ITranscriber
{
    private volatile string _last = primary.Name;

    /// <summary>The engine that did the last pass.</summary>
    public string Name => _last;

    public async Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken)
    {
        if (breaker.TryBeginAttempt())
        {
            try
            {
                var transcript = await primary.TranscribeAsync(request, progress, cancellationToken).ConfigureAwait(false);
                breaker.RecordSuccess();
                _last = primary.Name;
                await host.UnloadAsync(cancellationToken).ConfigureAwait(false);
                return transcript;
            }
            catch (TranscriptionException)
            {
                // The pass is not lost: it runs again on the model inside tawk-mcp.
                breaker.RecordFailure();
            }
        }

        var own = await fallback.TranscribeAsync(request, progress, cancellationToken).ConfigureAwait(false);
        _last = fallback.Name;
        return own;
    }
}
