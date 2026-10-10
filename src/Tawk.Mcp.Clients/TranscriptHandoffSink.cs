using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Managers;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Clients;

/// <summary>
/// When a transcription ends, hands each language's text to tawk, so the user reads it under the voice
/// note and it outlives tawk-mcp's own short memory of the job.
/// </summary>
public sealed class TranscriptHandoffSink(ITranscriptHandoff handoff, IAccountScope accounts) : IEventSink
{
    public async Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (update.Event is not TranscriptEvent { Job: var job })
        {
            return;
        }

        // The job ended outside the call that asked for it, so it names its account again.
        using (accounts.Use(job.Request.Account))
        {
            // A job writes the voice note out afresh: its first transcript takes the place of what tawk
            // kept before, in whatever language, and the rest of the job's languages are added beside it.
            var replace = true;
            foreach (var pass in job.Passes)
            {
                if (pass.Transcript is { } transcript)
                {
                    await handoff.HandOverAsync(job.Request.MessageId, pass.Language, transcript, replace, cancellationToken).ConfigureAwait(false);
                    replace = false;
                }
            }
        }
    }
}
