using System.Text;
using Microsoft.Extensions.Logging;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Whisper.net;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>The only type that touches Whisper's own factory. Everything about the model's life is here.</summary>
public sealed partial class WhisperModelHost : IWhisperModelHost, IAsyncDisposable
{
    // How often a pass that waits for a model to download says how much of it is here.
    private static readonly TimeSpan DownloadTick = TimeSpan.FromSeconds(1);

    private readonly IModelFiles _files;
    private readonly IModelLock _lock;
    private readonly IWhisperRuntime _runtime;
    private readonly IBackoffPolicy _backoff;
    private readonly TranscriptionOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<WhisperModelHost> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;
    private string? _loaded;
    private IDisposable? _held;
    private ITimer? _idle;
    private DateTimeOffset _lastUsed;
    private Task<string>? _fetch;
    private string? _fetching;

    public WhisperModelHost(
        IModelFiles files,
        IModelLock modelLock,
        IWhisperRuntime runtime,
        IBackoffPolicy backoff,
        TranscriptionOptions options,
        TimeProvider clock,
        ILogger<WhisperModelHost> logger)
    {
        _files = files;
        _lock = modelLock;
        _runtime = runtime;
        _backoff = backoff;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Transcript> TranscribeAsync(
        ReadOnlyMemory<float> samples, TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);
        if (_gate.CurrentCount == 0)
        {
            progress.Report(new TranscriptionProgress(TranscriptionStage.WaitingForModel));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                return await OnceAsync(samples, request, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TranscriptionException)
            {
                // A model that failed is not trusted again: it is let go and loaded afresh, once.
                LogRetry(request.Model, ex);
                Unload();
                await Task.Delay(_backoff.NextDelay(0), _clock, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await OnceAsync(samples, request, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TranscriptionException)
            {
                Unload();
                throw new TranscriptionException("engine error", ex);
            }
        }
        catch (OperationCanceledException)
        {
            // Native work that was cut off leaves the model in an unknown state.
            Unload();
            throw;
        }
        finally
        {
            _lastUsed = _clock.GetUtcNow();
            WatchForIdle();
            _gate.Release();
        }
    }

    public async Task UnloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Unload();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_idle is not null)
        {
            await _idle.DisposeAsync().ConfigureAwait(false);
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        Unload();
        _gate.Dispose();
    }

    private async Task<Transcript> OnceAsync(
        ReadOnlyMemory<float> samples, TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken)
    {
        var factory = await LoadAsync(request.Model, progress, cancellationToken).ConfigureAwait(false);
        var seconds = (double)samples.Length / OggOpusDecoder.SampleRate;

        // The engine counts in coarse steps and each segment says where it ends, so the furthest of the two is told.
        var percent = 0;
        void Heard(int reached)
        {
            percent = Math.Clamp(Math.Max(percent, reached), 0, 100);
            progress.Report(new TranscriptionProgress(TranscriptionStage.Transcribing, percent));
        }

        var builder = factory.CreateBuilder().WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 1, 8)).WithProgressHandler(Heard);
        builder = request.LanguageHint is { } language ? builder.WithLanguage(language) : builder.WithLanguageDetection();
        if (request.Task == TranscriptionTask.Translate)
        {
            builder = builder.WithTranslate();
        }

        if (request.Prompt is { Length: > 0 } prompt)
        {
            builder = builder.WithPrompt(prompt);
        }

        var text = new StringBuilder();
        string? heard = null;
        var processor = builder.Build();
        await using (processor.ConfigureAwait(false))
        {
            Heard(0);
            await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
            {
                text.Append(segment.Text);
                heard ??= segment.Language;
                if (seconds > 0)
                {
                    Heard((int)(segment.End.TotalSeconds / seconds * 100));
                }
            }
        }

        return new Transcript(
            text.ToString().Trim(),
            request.LanguageHint is null ? heard : null,
            request.Model,
            seconds);
    }

    // The model asked for, loaded. Another model is let go first, so two are never in memory together.
    private async Task<WhisperFactory> LoadAsync(string model, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken)
    {
        if (_factory is not null && string.Equals(_loaded, model, StringComparison.Ordinal))
        {
            return _factory;
        }

        Unload();
        var path = await PathAsync(model, progress, cancellationToken).ConfigureAwait(false);
        progress.Report(new TranscriptionProgress(TranscriptionStage.LoadingModel));
        _held = await _lock.AcquireAsync(_options.LockWait, cancellationToken).ConfigureAwait(false)
            ?? throw new TranscriptionException("transcriber busy: another tawk-mcp on this computer is transcribing");
        _runtime.Ensure();
        _factory = await Task.Run(() => WhisperFactory.FromPath(path), cancellationToken).ConfigureAwait(false);
        _loaded = model;
        LogLoaded(model);
        return _factory;
    }

    // The model's file, downloaded first when it is not here yet. The download is not tied to the pass that
    // asked for it: a large model outlasts one pass, so it carries on and the next pass picks it up.
    private async Task<string> PathAsync(string model, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken)
    {
        if (_files.Find(model) is { } installed)
        {
            return installed;
        }

        if (_fetch is null || _fetch.IsCompleted || !string.Equals(_fetching, model, StringComparison.Ordinal))
        {
            LogFetching(model);
            _fetching = model;
            _fetch = _files.FetchAsync(model, CancellationToken.None);
        }

        try
        {
            // The pass waits and says how much of the model is here each time it looks.
            while (true)
            {
                progress.Report(new TranscriptionProgress(TranscriptionStage.DownloadingModel, Bytes: _files.Downloaded(model)));
                var tick = Task.Delay(DownloadTick, _clock, cancellationToken);
                if (await Task.WhenAny(_fetch, tick).ConfigureAwait(false) == _fetch)
                {
                    return await _fetch.ConfigureAwait(false);
                }

                await tick.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!_fetch.IsCompleted)
        {
            throw new TranscriptionException($"the {model} model is still downloading: try again in a few minutes");
        }
    }

    private void Unload()
    {
        if (_factory is not null)
        {
            _factory.Dispose();
            LogUnloaded(_loaded ?? string.Empty);
        }

        _factory = null;
        _loaded = null;
        _held?.Dispose();
        _held = null;
    }

    private void WatchForIdle()
    {
        if (_options.IdleUnload <= TimeSpan.Zero || _idle is not null)
        {
            return;
        }

        var every = _options.IdleUnload < TimeSpan.FromMinutes(1) ? _options.IdleUnload : TimeSpan.FromMinutes(1);
        _idle = _clock.CreateTimer(_ => UnloadIfIdle(), null, every, every);
    }

    // Skipped while a job runs: the gate is only taken when it is free.
    private void UnloadIfIdle()
    {
        if (_factory is null || !_gate.Wait(0))
        {
            return;
        }

        try
        {
            if (_clock.GetUtcNow() - _lastUsed >= _options.IdleUnload)
            {
                Unload();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The {Model} transcription model is not installed and is being downloaded")]
    private partial void LogFetching(string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded the {Model} transcription model")]
    private partial void LogLoaded(string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Let go of the {Model} transcription model")]
    private partial void LogUnloaded(string model);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Model} transcription model failed and is loaded again")]
    private partial void LogRetry(string model, Exception exception);
}
