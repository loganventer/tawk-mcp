using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>The choices in tawk's settings panel, set by the test.</summary>
public sealed class FakeTranscriptionPreferences : ITranscriptionPreferences
{
    public TranscriptionPreferences Chosen { get; set; } = TranscriptionPreferences.None;

    public Task<TranscriptionPreferences> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Chosen);
}
