using System.Globalization;
using System.Text;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

public sealed class MemoryFormatter : IMemoryFormatter
{
    public string Categories(IReadOnlyList<AudienceCategory> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        if (categories.Count == 0)
        {
            return "No categories yet. Add one with set_category, such as family/spouse or work/peers.";
        }

        return string.Join('\n', categories.Select(c => c.Description is null ? c.Path : $"{c.Path}: {c.Description}"));
    }

    public string Voices(IReadOnlyList<Voice> voices)
    {
        ArgumentNullException.ThrowIfNull(voices);
        if (voices.Count == 0)
        {
            return "No voices yet. Add one with set_voice or import_voice.";
        }

        return string.Join('\n', voices.Select(v => v.Name + (v.IsDefault ? " (default)" : string.Empty) + (v.Description is null ? string.Empty : ": " + v.Description)));
    }

    public string Voice(Voice voice, IReadOnlyList<VoiceVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(variants);
        var text = new StringBuilder();
        text.Append("Voice ").Append(voice.Name).Append(voice.IsDefault ? " (default)" : string.Empty).Append('\n');
        if (voice.Description is not null)
        {
            text.Append(voice.Description).Append('\n');
        }

        text.Append("Rules: ").Append(RulesJson.Write(voice.Rules)).Append('\n');
        text.Append("Audience variants: ").Append(variants.Count == 0 ? "none" : string.Join(", ", variants.Select(v => v.Category))).Append("\n\n");
        text.Append(voice.Guide);
        return text.ToString();
    }

    public string Resolved(ResolvedVoice resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        var text = new StringBuilder();
        text.Append("Write as voice ").Append(resolved.Voice.Name);
        text.Append(resolved.Variant is null ? " (no audience variant matched, so the base guide applies)" : $" for audience {resolved.Variant.Category}");
        text.Append(".\nRules in force: ").Append(RulesJson.Write(resolved.Rules)).Append('\n');
        if (resolved.Variant is not null)
        {
            text.Append("\n## For this audience\n").Append(resolved.Variant.Guide).Append('\n');
            if (resolved.Variant.Examples.Count > 0)
            {
                text.Append("\nExamples of how the user writes to this audience:\n");
                foreach (var example in resolved.Variant.Examples)
                {
                    text.Append("- ").Append(example).Append('\n');
                }
            }
        }

        text.Append("\n## Everywhere\n").Append(resolved.Voice.Guide);
        return text.ToString();
    }

    public string Report(VoiceReport report, ResolvedVoice resolved)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(resolved);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Score {report.Score}/100 against voice {resolved.Voice.Name}");
        text.Append(resolved.Variant is null ? string.Empty : $" for {resolved.Variant.Category}").Append(".\n");
        var f = report.Features;
        text.Append(CultureInfo.InvariantCulture, $"Measured: {f.WordCount} words, language {f.Language}, {f.Emoji.Count} emoji, ");
        text.Append(f.AllLowercase ? "all lowercase" : f.StartsUppercase ? "starts with a capital" : "starts in lowercase").Append(".\n");
        if (report.Findings.Count == 0)
        {
            text.Append("No rule findings.\n");
        }
        else
        {
            foreach (var finding in report.Findings)
            {
                text.Append("- [").Append(finding.Severity.ToString().ToLowerInvariant()).Append("] ").Append(finding.Rule).Append(": ").Append(finding.Message).Append('\n');
            }
        }

        text.Append("\nThe rules only catch what can be counted. Read the guide below and judge the tone yourself before showing the draft.\n\n");
        text.Append(Resolved(resolved));
        return text.ToString();
    }

    public string ExportMarkdown(Voice voice, IReadOnlyList<VoiceVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(variants);
        var text = new StringBuilder(voice.Guide.TrimEnd()).Append("\n\n");
        foreach (var variant in variants)
        {
            text.Append("### ").Append(variant.Category).Append("\n\n").Append(variant.Guide.TrimEnd()).Append("\n\n");
        }

        return text.ToString().TrimEnd() + "\n";
    }

    public string Contact(ContactRecord contact, IReadOnlyList<string> categories, IReadOnlyList<ContactFact> facts, IReadOnlyList<ContactNote> notes, int hiddenSensitive)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(notes);
        var text = new StringBuilder();
        text.Append("Contact ").Append(contact.DisplayName ?? contact.Jid).Append(" <").Append(contact.Jid).Append(">\n");
        text.Append("Categories: ").Append(categories.Count == 0 ? "none" : string.Join(", ", categories)).Append('\n');
        text.Append("Voice: ").Append(contact.Voice ?? "the default").Append('\n');
        text.Append("Fields (source, confidence):\n");
        if (facts.Count == 0)
        {
            text.Append("  none\n");
        }

        foreach (var fact in facts)
        {
            text.Append("  ").Append(fact.Field).Append(": ").Append(fact.ValueJson).Append(" (").Append(Source(fact.Source));
            if (fact.Source is FactSource.Inferred or FactSource.Imported)
            {
                text.Append(CultureInfo.InvariantCulture, $", {fact.Confidence:0.00}");
            }

            if (fact.Expires is { } expires)
            {
                text.Append(CultureInfo.InvariantCulture, $", until {expires:yyyy-MM-dd}");
            }

            text.Append(")\n");
        }

        if (hiddenSensitive > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {hiddenSensitive} sensitive field(s) hidden; ask with include_sensitive only if the user wants them.\n");
        }

        text.Append("Notes:\n");
        if (notes.Count == 0)
        {
            text.Append("  none\n");
        }

        foreach (var note in notes)
        {
            text.Append(CultureInfo.InvariantCulture, $"  [{note.Id}] {note.Created:yyyy-MM-dd} ({Source(note.Source)}): {note.Text}\n");
        }

        text.Append("Inferred values are guesses from chats, not facts: use them to set tone, never to make claims about the person.");
        return text.ToString();
    }

    public string Contacts(IReadOnlyList<ContactRecord> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        return contacts.Count == 0
            ? "No contacts in memory match."
            : string.Join('\n', contacts.Select(c => $"{c.DisplayName ?? c.Jid} <{c.Jid}>"));
    }

    public string Fields(IReadOnlyList<ContactFieldDefinition> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var text = new StringBuilder("Profile fields, by group. Values are JSON. Anything else goes in a note.\n");
        foreach (var group in fields.GroupBy(f => f.Group))
        {
            text.Append('\n').Append(group.Key).Append(":\n");
            foreach (var field in group)
            {
                text.Append("  ").Append(field.Name).Append(" (").Append(Kind(field)).Append(')');
                if (!field.Inferable)
                {
                    text.Append(field.Sensitive ? " [user only]" : " [stated only, never inferred]");
                }

                text.Append(": ").Append(field.Description).Append('\n');
            }
        }

        return text.ToString().TrimEnd();
    }

    public string Templates(IReadOnlyList<ResponseTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        return templates.Count == 0
            ? "No templates yet. Add one with set_template."
            : string.Join('\n', templates.Select(t => t.Name + (t.Category is null ? string.Empty : $" [{t.Category}]") + (t.Description is null ? string.Empty : ": " + t.Description)));
    }

    public string Template(ResponseTemplate responseTemplate, IReadOnlyList<string> placeholders)
    {
        ArgumentNullException.ThrowIfNull(responseTemplate);
        ArgumentNullException.ThrowIfNull(placeholders);
        var text = new StringBuilder();
        text.Append("Template ").Append(responseTemplate.Name).Append('\n');
        if (responseTemplate.Description is not null)
        {
            text.Append(responseTemplate.Description).Append('\n');
        }

        text.Append("Category: ").Append(responseTemplate.Category ?? "any").Append(", voice: ").Append(responseTemplate.Voice ?? "the default");
        text.Append(", language: ").Append(responseTemplate.Language ?? "any").Append('\n');
        text.Append("Placeholders: ").Append(placeholders.Count == 0 ? "none" : string.Join(", ", placeholders)).Append("\n\n");
        text.Append(responseTemplate.Body);
        return text.ToString();
    }

    private static string Source(FactSource source) => source.ToString().ToLowerInvariant();

    private static string Kind(ContactFieldDefinition field) => field.Kind switch
    {
        FieldKind.Choice => "one of " + string.Join("|", field.Choices ?? []),
        FieldKind.ChoiceList => "list of " + string.Join("|", field.Choices ?? []),
        FieldKind.Number or FieldKind.WholeNumber => string.Create(CultureInfo.InvariantCulture, $"{(field.Kind == FieldKind.WholeNumber ? "whole number" : "number")} {field.Min}..{field.Max}"),
        FieldKind.TextList => "list of text",
        FieldKind.Json => "JSON",
        _ => field.Kind.ToString().ToLowerInvariant(),
    };
}
