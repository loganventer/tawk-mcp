using System.Text.Json;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Reads the choices with get_settings and keeps them for a few seconds, so a run of voice notes does not
/// ask tawk again for each one while a change in the panel still takes hold quickly.
/// </summary>
public sealed class TawkTranscriptionPreferences(ITawkControl control, TimeProvider clock) : ITranscriptionPreferences
{
    public const string Section = "automation";

    private static readonly TimeSpan KeptFor = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private TranscriptionPreferences _last = TranscriptionPreferences.None;
    private DateTimeOffset _readAt = DateTimeOffset.MinValue;

    public async Task<TranscriptionPreferences> ReadAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (clock.GetUtcNow() - _readAt < KeptFor)
            {
                return _last;
            }
        }

        TranscriptionPreferences read;
        try
        {
            read = Parse(await control.RequestAsync("get_settings", null, cancellationToken).ConfigureAwait(false));
        }
        catch (TawkControlException)
        {
            // tawk is down or will not say: the flags and the defaults stand in.
            return TranscriptionPreferences.None;
        }

        lock (_gate)
        {
            _last = read;
            _readAt = clock.GetUtcNow();
        }

        return read;
    }

    private static TranscriptionPreferences Parse(JsonElement result)
    {
        string? model = null, languages = null;
        bool? automatic = null;
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Array)
        {
            return TranscriptionPreferences.None;
        }

        foreach (var setting in settings.EnumerateArray())
        {
            if (!string.Equals(Text(setting, "section"), Section, StringComparison.Ordinal) || Text(setting, "value") is not { } value)
            {
                continue;
            }

            switch (Text(setting, "key"))
            {
                case "transcribe_model":
                    model = value.Trim().Length == 0 ? null : value.Trim();
                    break;
                case "transcribe_languages":
                    languages = value.Trim().Length == 0 ? null : value.Trim();
                    break;
                case "transcribe_auto":
                    automatic = value.Trim().ToUpperInvariant() is "TRUE" or "ON" or "1" or "YES";
                    break;
            }
        }

        return new TranscriptionPreferences(model, languages, automatic);
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
