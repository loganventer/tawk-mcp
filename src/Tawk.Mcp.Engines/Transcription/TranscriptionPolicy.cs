using System.Globalization;
using System.Text.RegularExpressions;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.Engines.Transcription;

public sealed partial class TranscriptionPolicy(TranscriptionOptions options) : ITranscriptionPolicy
{
    public const int MaxPromptLength = 500;

    public TranscriptionRequest Resolve(
        string messageId,
        string? account,
        IReadOnlyList<string>? languages,
        string? task,
        string? model,
        string? prompt,
        TranscriptionPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new TranscriptionException("A message id is needed.");
        }

        return new TranscriptionRequest(
            messageId.Trim(),
            string.IsNullOrWhiteSpace(account) ? null : account.Trim(),
            Languages(languages, preferences),
            Task(task),
            Model(model, preferences),
            Prompt(prompt));
    }

    public bool Automatic(TranscriptionPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.Automatic ?? options.Automatic ?? false;
    }

    /// <summary>The language as a code in lower case, or null when it is not one.</summary>
    public static string? Language(string? value)
    {
        var code = value?.Trim().ToLowerInvariant();
        return code is not null && (code == TranscriptionOptions.Auto || Code().IsMatch(code)) ? code : null;
    }

    // What is typed into tawk's panel is free text, so anything in it that is not a language is passed over.
    private static List<string>? FromPanel(string? languages)
    {
        var codes = (languages ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Language)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return codes.Count == 0 ? null : codes;
    }

    private List<string> Languages(IReadOnlyList<string>? asked, TranscriptionPreferences preferences)
    {
        if (asked is not { Count: > 0 })
        {
            var defaults = FromPanel(preferences.Languages) ?? options.Languages ?? [TranscriptionOptions.Auto];
            return [.. defaults.Take(options.MaxLanguages)];
        }

        var wanted = asked;
        var codes = new List<string>();
        foreach (var value in wanted)
        {
            var code = Language(value)
                ?? throw new TranscriptionException($"\"{PlainName.Of(value ?? string.Empty)}\" is not a language. Use ISO 639-1 codes such as en or af, or auto.");
            if (!codes.Contains(code, StringComparer.Ordinal))
            {
                codes.Add(code);
            }
        }

        if (codes.Count > options.MaxLanguages)
        {
            throw new TranscriptionException(string.Create(
                CultureInfo.InvariantCulture, $"At most {options.MaxLanguages} languages in one call, and {codes.Count} were asked for."));
        }

        return codes;
    }

    private static TranscriptionTask Task(string? task) => task?.Trim().ToUpperInvariant() switch
    {
        null or "" or "TRANSCRIBE" => TranscriptionTask.Transcribe,
        "TRANSLATE" => TranscriptionTask.Translate,
        _ => throw new TranscriptionException("The task must be transcribe or translate."),
    };

    private string Model(string? model, TranscriptionPreferences preferences)
    {
        var chosen = preferences.Model ?? options.Model ?? TranscriptionOptions.DefaultModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            return chosen;
        }

        var name = model.Trim();
        if (string.Equals(name, chosen, StringComparison.Ordinal) || options.Models.Contains(name, StringComparer.Ordinal))
        {
            return name;
        }

        var allowed = options.Models.Prepend(chosen).Distinct(StringComparer.Ordinal);
        throw new TranscriptionException($"That model is not allowed. The user allows: {string.Join(", ", allowed)}.");
    }

    private static string? Prompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var text = prompt.ReplaceLineEndings(" ").Trim();
        return text.Length <= MaxPromptLength
            ? text
            : throw new TranscriptionException(string.Create(CultureInfo.InvariantCulture, $"The prompt may be at most {MaxPromptLength} characters."));
    }

    [GeneratedRegex("^[a-z]{2,3}$")]
    private static partial Regex Code();
}
