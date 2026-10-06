using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Answers each language from a table, records every pass, and can fail or stall a language.</summary>
public sealed class FakeTranscriber : ITranscriber
{
    public string Name => "fake";

    public List<TranscriptionPassRequest> Passes { get; } = [];

    public Dictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Failures { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Stalls { get; } = new(StringComparer.Ordinal);

    public string? Heard { get; set; }

    public double? DurationS { get; set; }

    /// <summary>What each pass reports before it does anything else.</summary>
    public TranscriptionProgress? Says { get; set; }

    public async Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken)
    {
        Passes.Add(request);
        if (Says is not null)
        {
            progress.Report(Says);
        }

        if (Stalls.Contains(request.Language))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (Failures.TryGetValue(request.Language, out var why))
        {
            throw new TranscriptionException(why);
        }

        return new Transcript(
            Texts.GetValueOrDefault(request.Language, $"text in {request.Language}"), request.LanguageHint is null ? Heard : null, request.Model, DurationS);
    }
}
