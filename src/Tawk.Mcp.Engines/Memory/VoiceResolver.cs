using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

public sealed class VoiceResolver : IVoiceResolver
{
    public ResolvedVoice Resolve(Voice voice, IReadOnlyList<VoiceVariant> variants, IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(categories);
        foreach (var category in categories)
        {
            for (var path = category.Trim('/'); path.Length > 0; path = Parent(path))
            {
                var variant = variants.FirstOrDefault(v => string.Equals(v.Category, path, StringComparison.OrdinalIgnoreCase));
                if (variant is not null)
                {
                    return new ResolvedVoice(voice, variant, Overlay(voice.Rules, variant.Rules));
                }
            }
        }

        return new ResolvedVoice(voice, null, voice.Rules);
    }

    private static string Parent(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    // A variant's rule replaces the voice's rule of the same kind; rules it leaves unset are kept.
    private static VoiceRules Overlay(VoiceRules basis, VoiceRules variant) => new()
    {
        Languages = variant.Languages ?? basis.Languages,
        Case = variant.Case ?? basis.Case,
        MaxWords = variant.MaxWords ?? basis.MaxWords,
        MaxEmoji = variant.MaxEmoji ?? basis.MaxEmoji,
        AllowedEmoji = variant.AllowedEmoji ?? basis.AllowedEmoji,
        RequiredAddressForms = variant.RequiredAddressForms ?? basis.RequiredAddressForms,
        ForbiddenAddressForms = variant.ForbiddenAddressForms ?? basis.ForbiddenAddressForms,
        MustIncludeAny = variant.MustIncludeAny ?? basis.MustIncludeAny,
        ForbiddenPatterns = variant.ForbiddenPatterns ?? basis.ForbiddenPatterns,
        Greeting = variant.Greeting ?? basis.Greeting,
        Signoff = variant.Signoff ?? basis.Signoff,
        Baseline = variant.Baseline ?? basis.Baseline,
    };
}
