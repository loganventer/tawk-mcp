namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>Turns a voice note file into the samples a Whisper model takes: 16 kHz, one channel.</summary>
public interface IAudioDecoder
{
    /// <summary>
    /// Decodes the whole file. Throws <see cref="Core.Transcription.TranscriptionException"/> for a format it
    /// does not know or a recording longer than <paramref name="maxSeconds"/>.
    /// </summary>
    float[] Decode(string path, int maxSeconds);
}
