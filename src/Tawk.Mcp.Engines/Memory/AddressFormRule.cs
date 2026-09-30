using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Checks how the person is addressed, such as oom or tannie instead of jy for an elder.</summary>
public sealed class AddressFormRule : IVoiceRule
{
    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.RequiredAddressForms is { Count: > 0 } required && !required.Any(form => WordMatch.Contains(draft.Text, form)))
        {
            yield return new VoiceFinding(
                "address",
                FindingSeverity.Warning,
                $"Does not address them as {string.Join(" or ", required)}, as this voice does with this audience.");
        }

        foreach (var form in rules.ForbiddenAddressForms ?? [])
        {
            if (WordMatch.Contains(draft.Text, form))
            {
                yield return new VoiceFinding("address", FindingSeverity.Error, $"Uses \"{form}\", which is wrong for this audience.");
            }
        }
    }
}
