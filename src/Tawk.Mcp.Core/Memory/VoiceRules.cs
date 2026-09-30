namespace Tawk.Mcp.Core.Memory;

/// <summary>Checkable style rules. Every rule is optional; a variant's rules override the voice's one by one.</summary>
public sealed record VoiceRules
{
    public static VoiceRules None { get; } = new();

    /// <summary>Languages a draft may be in: af, en, or mix.</summary>
    public IReadOnlyList<string>? Languages { get; init; }

    /// <summary>lower, sentence or any.</summary>
    public string? Case { get; init; }

    public int? MaxWords { get; init; }

    public int? MaxEmoji { get; init; }

    public IReadOnlyList<string>? AllowedEmoji { get; init; }

    /// <summary>At least one of these forms of address must appear, such as oom or tannie.</summary>
    public IReadOnlyList<string>? RequiredAddressForms { get; init; }

    public IReadOnlyList<string>? ForbiddenAddressForms { get; init; }

    /// <summary>At least one of these words must appear, such as asb or dankie.</summary>
    public IReadOnlyList<string>? MustIncludeAny { get; init; }

    /// <summary>Regular expressions that must not match, such as an em dash or "Kind regards".</summary>
    public IReadOnlyList<string>? ForbiddenPatterns { get; init; }

    /// <summary>none, optional or required.</summary>
    public string? Greeting { get; init; }

    /// <summary>none, optional or required.</summary>
    public string? Signoff { get; init; }

    public StyleBaseline? Baseline { get; init; }
}
