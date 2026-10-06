using System.Globalization;
using System.Text;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.Engines.Transcription;

public sealed class TranscriptionNoticeFormatter(IUntrustedTextFence fence) : ITranscriptionNoticeFormatter
{
    public string Started(TranscriptionJob job, bool joined)
    {
        ArgumentNullException.ThrowIfNull(job);
        var languages = string.Join(", ", job.Request.Languages);
        return joined
            ? $"That transcription is already under way as job {job.Id} ({languages}). A channel event follows when it ends; do not ask again. get_transcript reads it if your client has no channel."
            : $"Transcribing message {PlainName.Of(job.Request.MessageId)} as job {job.Id} ({languages}). A channel event follows when it ends; do not wait or ask again. get_transcript reads it if your client has no channel.";
    }

    public string Describe(TranscriptionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var text = new StringBuilder();

        // The account goes first, outside the fenced text, so nothing spoken can pose as it.
        if (job.Request.Account is { } account)
        {
            text.Append("On the user's account \"").Append(PlainName.Of(account)).Append("\"; pass that account when you act on this.\n");
        }

        var chat = job.Chat is null ? string.Empty : $" in \"{PlainName.Of(job.Chat.Name)}\"";
        text.Append("Transcript of the voice note").Append(chat)
            .Append(" (message id ").Append(PlainName.Of(job.Request.MessageId)).Append(", job ").Append(job.Id).Append("): ")
            .Append(State(job));
        if (job.Failure is not null)
        {
            text.Append(" (").Append(job.Failure).Append(')');
        }

        text.Append('.');
        foreach (var pass in job.Passes.Where(p => p.Ended))
        {
            text.Append('\n').Append(Label(pass));
            if (pass.Transcript is { } transcript)
            {
                text.Append('\n').Append(fence.Wrap(
                    "a transcript of a WhatsApp voice note",
                    string.IsNullOrWhiteSpace(transcript.Text) ? "(nothing was heard)" : transcript.Text));
            }
        }

        return text.ToString();
    }

    public string Progress(TranscriptionJob job, TranscriptionProgress? progress, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(job);
        var text = new StringBuilder();
        text.Append("Job ").Append(job.Id).Append(" (message id ").Append(PlainName.Of(job.Request.MessageId))
            .Append(", model ").Append(PlainName.Of(job.Request.Model)).Append("): ").Append(State(job));
        if (job.Ended)
        {
            return text.Append(". get_transcript reads it.").ToString();
        }

        var asked = Math.Max(0, (long)(now - job.CreatedAt).TotalSeconds);
        text.Append(CultureInfo.InvariantCulture, $", asked {asked} s ago.");
        if (job.State != TranscriptionState.Running)
        {
            return text.ToString();
        }

        if (progress?.Language is { } language)
        {
            var pass = Math.Min(job.Passes.Count(p => p.Ended) + 1, job.Passes.Count);
            text.Append(CultureInfo.InvariantCulture, $" Language {PlainName.Of(language)}, {pass} of {job.Passes.Count}:");
        }

        return text.Append(' ').Append(Step(job, progress)).Append('.').ToString();
    }

    private static string Step(TranscriptionJob job, TranscriptionProgress? progress)
    {
        var model = PlainName.Of(job.Request.Model);
        return progress switch
        {
            null => "starting",
            { Stage: TranscriptionStage.FetchingAudio } => "fetching the voice note from tawk",
            { Stage: TranscriptionStage.DecodingAudio } => "decoding the voice note",
            { Stage: TranscriptionStage.WaitingForModel } => "waiting for another transcription to finish",
            { Stage: TranscriptionStage.DownloadingModel, Bytes: { } bytes } =>
                string.Create(CultureInfo.InvariantCulture, $"downloading the {model} model, {bytes / (1024 * 1024)} MB so far"),
            { Stage: TranscriptionStage.DownloadingModel } => $"downloading the {model} model",
            { Stage: TranscriptionStage.LoadingModel } => $"loading the {model} model",
            { Percent: { } percent } => string.Create(CultureInfo.InvariantCulture, $"transcribing, {percent}% of the voice note heard"),
            _ => "transcribing",
        };
    }

    private static string State(TranscriptionJob job) => job.State switch
    {
        TranscriptionState.Queued => "queued",
        TranscriptionState.Running => "running",
        TranscriptionState.Done => "done",
        TranscriptionState.Partial => "partial",
        _ => "failed",
    };

    // The label is tawk-mcp's own line, written outside the fence.
    private static string Label(TranscriptionPass pass)
    {
        if (pass.Failure is not null)
        {
            return $"Language {pass.Language}: failed ({pass.Failure}).";
        }

        var heard = pass.Transcript?.DetectedLanguage;
        return pass.Language == TranscriptionOptions.Auto && !string.IsNullOrWhiteSpace(heard)
            ? $"Language auto (heard: {PlainName.Of(heard)}):"
            : $"Language {pass.Language}:";
    }
}
