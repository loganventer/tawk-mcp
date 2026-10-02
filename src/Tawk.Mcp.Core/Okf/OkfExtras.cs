using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Core.Okf;

/// <summary>Reads and writes the producer keys a concept carries: source, confidence, sensitive and evidence.</summary>
public static class OkfExtras
{
    public static string Write(ObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var extra = new JsonObject { ["source"] = SourceName(details.Source) };
        if (details.Confidence < 1.0)
        {
            extra["confidence"] = Math.Round(details.Confidence, 2);
        }

        if (details.Sensitive)
        {
            extra["sensitive"] = true;
        }

        if (!string.IsNullOrWhiteSpace(details.Evidence))
        {
            extra["evidence"] = details.Evidence;
        }

        return extra.ToJsonString();
    }

    /// <summary>The details of a concept. One without a source key is ranked by who generated it.</summary>
    public static ObservationDetails Read(OkfConcept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);
        var source = OkfActors.SourceOf(concept.GeneratedBy);
        var confidence = 1.0;
        var sensitive = false;
        string? evidence = null;
        try
        {
            using var document = JsonDocument.Parse(concept.ExtraJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var root = document.RootElement;
                if (root.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String
                    && Enum.TryParse<FactSource>(s.GetString(), true, out var parsed) && Enum.IsDefined(parsed))
                {
                    source = parsed;
                }

                if (root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number)
                {
                    confidence = Math.Clamp(c.GetDouble(), 0, 1);
                }

                sensitive = root.TryGetProperty("sensitive", out var flag) && flag.ValueKind == JsonValueKind.True;
                evidence = root.TryGetProperty("evidence", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            }
        }
        catch (JsonException)
        {
            // Producer keys that do not parse are treated as absent.
        }

        return new ObservationDetails(source, confidence, sensitive, evidence);
    }

    public static string SourceName(FactSource source) => source switch
    {
        FactSource.User => "user",
        FactSource.Contact => "contact",
        FactSource.Imported => "imported",
        _ => "inferred",
    };
}
