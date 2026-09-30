namespace Tawk.Mcp.Engines.Memory;

public interface IVoiceGuideImporter
{
    /// <summary>
    /// Splits a Markdown guide by heading. Each heading named in <paramref name="sections"/> becomes that category's
    /// variant, with everything under it; the rest stays in the base guide.
    /// </summary>
    ImportedVoiceGuide Split(string markdown, IReadOnlyDictionary<string, string> sections);
}
