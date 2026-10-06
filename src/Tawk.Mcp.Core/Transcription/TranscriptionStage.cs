namespace Tawk.Mcp.Core.Transcription;

/// <summary>The step a running transcription is on.</summary>
public enum TranscriptionStage
{
    /// <summary>The voice note is being fetched from tawk.</summary>
    FetchingAudio,

    DecodingAudio,

    /// <summary>Another transcription has the model, and this one waits its turn.</summary>
    WaitingForModel,

    DownloadingModel,

    LoadingModel,

    Transcribing,
}
