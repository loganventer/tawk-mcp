namespace Tawk.Mcp.Core.Transcription;

/// <summary>A transcription that was refused or failed. The message is short and safe to show a model.</summary>
public sealed class TranscriptionException : Exception
{
    public TranscriptionException()
        : this("The transcription failed.")
    {
    }

    public TranscriptionException(string message)
        : base(message)
    {
    }

    public TranscriptionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
