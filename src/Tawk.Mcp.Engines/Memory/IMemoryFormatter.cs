using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Turns stored memory into model-ready text. Formatting only; fencing is the caller's job.</summary>
public interface IMemoryFormatter
{
    string Categories(IReadOnlyList<AudienceCategory> categories);

    string Voices(IReadOnlyList<Voice> voices);

    string Voice(Voice voice, IReadOnlyList<VoiceVariant> variants);

    string Resolved(ResolvedVoice resolved);

    string Report(VoiceReport report, ResolvedVoice resolved);

    string ExportMarkdown(Voice voice, IReadOnlyList<VoiceVariant> variants);

    string Contact(ContactRecord contact, IReadOnlyList<string> categories, IReadOnlyList<ContactFact> facts, IReadOnlyList<ContactNote> notes, int hiddenSensitive);

    string Contacts(IReadOnlyList<ContactRecord> contacts);

    string Fields(IReadOnlyList<ContactFieldDefinition> fields);

    string Templates(IReadOnlyList<ResponseTemplate> templates);

    string Template(ResponseTemplate responseTemplate, IReadOnlyList<string> placeholders);
}
