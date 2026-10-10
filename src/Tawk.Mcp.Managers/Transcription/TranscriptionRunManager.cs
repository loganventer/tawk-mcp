using Microsoft.Extensions.Logging;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Media;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Engines.Transcription;
using Tawk.Mcp.ResourceAccess.Media;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Managers.Transcription;

public sealed partial class TranscriptionRunManager : ITranscriptionRunManager
{
    public const string ChatSwitchedOff =
        "voice notes in this chat are not transcribed: the user switched that off on the chat's contact card in tawk";

    private readonly ITranscriptionJobStore _jobs;
    private readonly ITawkMediaSource _media;
    private readonly IMediaFiles _files;
    private readonly ITranscriber _transcriber;
    private readonly ITranscriptionNoticeFormatter _notices;
    private readonly IReadOnlyList<IEventSink> _sinks;
    private readonly IAccountScope _accounts;
    private readonly TranscriptionOptions _options;
    private readonly MediaOptions _mediaOptions;
    private readonly TimeProvider _clock;
    private readonly ILogger<TranscriptionRunManager> _logger;

    public TranscriptionRunManager(
        ITranscriptionJobStore jobs,
        ITawkMediaSource media,
        IMediaFiles files,
        ITranscriber transcriber,
        ITranscriptionNoticeFormatter notices,
        IEnumerable<IEventSink> sinks,
        IAccountScope accounts,
        TranscriptionOptions options,
        MediaOptions mediaOptions,
        TimeProvider clock,
        ILogger<TranscriptionRunManager> logger)
    {
        _jobs = jobs;
        _media = media;
        _files = files;
        _transcriber = transcriber;
        _notices = notices;
        _sinks = [.. sinks];
        _accounts = accounts;
        _options = options;
        _mediaOptions = mediaOptions;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunNextAsync(CancellationToken cancellationToken)
    {
        var job = await _jobs.TakeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The job runs outside the tool call that asked for it, so it names its account again.
            using (_accounts.Use(job.Request.Account))
            {
                job = await RunAsync(job, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogJobFailed(job.Id, ex);
            job = End(job, "engine error");
        }

        _jobs.Save(job);
        await AnnounceAsync(job, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TranscriptionJob> RunAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        MediaFile? file;
        try
        {
            _jobs.Report(job.Id, new TranscriptionProgress(TranscriptionStage.FetchingAudio));
            file = await _media.LocateAsync(job.Request.MessageId, _options.Timeout, cancellationToken).ConfigureAwait(false);
            if (file is null)
            {
                return End(job, "download failed");
            }

            if (file.Type is { Length: > 0 } type && type is not ("audio" or "video"))
            {
                return End(job with { Chat = file.Chat }, $"that message is {type}, not a voice note");
            }

            if (!file.Transcribe)
            {
                // The user switched this chat off in tawk: the audio is not handed to any model.
                return End(job with { Chat = file.Chat }, ChatSwitchedOff);
            }

            _files.Check(file.Path, _mediaOptions.MaxAudioBytes);
        }
        catch (MediaException ex)
        {
            return End(job, ex.Message);
        }
        catch (TawkControlException ex)
        {
            return End(job, ex.Message);
        }

        job = job with { Chat = file.Chat };
        var passes = job.Passes.ToList();
        for (var i = 0; i < passes.Count; i++)
        {
            passes[i] = await PassAsync(job, file.Path, passes[i], cancellationToken).ConfigureAwait(false);
            job = job with { Passes = [.. passes] };
            _jobs.Save(job);
        }

        return End(job, null);
    }

    // One language. A pass that fails says why and leaves the others to run.
    private async Task<TranscriptionPass> PassAsync(
        TranscriptionJob job, string path, TranscriptionPass pass, CancellationToken cancellationToken)
    {
        var request = job.Request;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_options.Timeout);
        try
        {
            var transcript = await _transcriber
                .TranscribeAsync(
                    new TranscriptionPassRequest(path, pass.Language, request.Task, request.Model, request.Prompt),
                    new PassProgress(_jobs, job.Id, pass.Language),
                    limit.Token)
                .ConfigureAwait(false);
            return transcript.DurationS > _options.MaxSeconds
                ? pass with { Failure = "too long" }
                : pass with { Transcript = transcript };
        }
        catch (TranscriptionException ex)
        {
            return pass with { Failure = ex.Message };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return pass with { Failure = "timed out" };
        }
    }

    private TranscriptionJob End(TranscriptionJob job, string? failure)
    {
        var done = job.Passes.Count(p => p.Transcript is not null);
        var state = failure is not null || done == 0
            ? TranscriptionState.Failed
            : done == job.Passes.Count ? TranscriptionState.Done : TranscriptionState.Partial;
        return job with { State = state, Failure = failure, EndedAt = _clock.GetUtcNow() };
    }

    private async Task AnnounceAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        var update = new LiveUpdate(new TranscriptEvent(job, _transcriber.Name), _notices.Describe(job));
        foreach (var sink in _sinks)
        {
            try
            {
                await sink.OnUpdateAsync(update, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSinkFailed(sink.GetType().Name, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transcription job {Job} failed")]
    private partial void LogJobFailed(string job, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Sink} update sink failed")]
    private partial void LogSinkFailed(string sink, Exception exception);
}
