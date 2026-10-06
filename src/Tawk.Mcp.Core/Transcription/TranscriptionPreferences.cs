namespace Tawk.Mcp.Core.Transcription;

/// <summary>
/// What the user chose in tawk's settings panel (Settings, Automation, Voice note transcription). Each is
/// null when tawk does not say, as with a tawk from before these settings or one that is not running.
/// </summary>
public sealed record TranscriptionPreferences(string? Model, string? Languages, bool? Automatic)
{
    public static TranscriptionPreferences None { get; } = new(null, null, null);
}
