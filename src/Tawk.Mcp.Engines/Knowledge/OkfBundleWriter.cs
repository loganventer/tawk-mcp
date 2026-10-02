using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

public sealed class OkfBundleWriter : IOkfBundleWriter
{
    private const int SummaryLength = 80;

    public OkfBundleWriting Write(OkfBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var concepts = bundle.Concepts.Where(c => OkfIds.IsPortable(c.Id)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var titles = concepts.ToDictionary(c => c.Id, c => c.Title ?? c.Id, StringComparer.Ordinal);
        var links = bundle.Links.ToLookup(l => l.FromId, StringComparer.Ordinal);
        var facts = bundle.Facts.ToLookup(f => OkfIds.Contact(f.Jid), StringComparer.Ordinal);

        var files = new List<OkfBundleFile> { new(OkfBundleKeys.IndexFile, Index(concepts)) };
        foreach (var concept in concepts)
        {
            files.Add(new OkfBundleFile(concept.Id + ".md", Document(concept, links[concept.Id].ToList(), facts[concept.Id].ToList(), titles)));
        }

        return new OkfBundleWriting(files, bundle.Concepts.Where(c => !OkfIds.IsPortable(c.Id)).Select(c => c.Id).ToList());
    }

    private static string Document(OkfConcept concept, List<OkfLink> links, List<ContactFact> facts, Dictionary<string, string> titles)
    {
        var front = new JsonObject { [OkfBundleKeys.Type] = concept.Type };
        Add(front, OkfBundleKeys.Title, concept.Title);
        Add(front, OkfBundleKeys.Description, concept.Description);
        Add(front, OkfBundleKeys.Resource, concept.Resource);
        if (concept.Tags.Count > 0)
        {
            front[OkfBundleKeys.Tags] = new JsonArray(concept.Tags.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
        }

        front[OkfBundleKeys.Generated] = new JsonObject { ["by"] = concept.GeneratedBy, ["at"] = OkfTimestamps.Format(concept.GeneratedAt) };
        if (concept.Verified.Count > 0)
        {
            front[OkfBundleKeys.Verified] = new JsonArray(
                concept.Verified.Select(v => (JsonNode?)new JsonObject { ["by"] = v.By, ["at"] = OkfTimestamps.Format(v.At) }).ToArray());
        }

        front[OkfBundleKeys.Status] = concept.Status switch { OkfStatus.Draft => "draft", OkfStatus.Deprecated => "deprecated", _ => "stable" };
        if (concept.StaleAfter is { } stale)
        {
            front[OkfBundleKeys.StaleAfter] = OkfTimestamps.Format(stale);
        }

        if (concept.Sources.Count > 0)
        {
            front[OkfBundleKeys.Sources] = new JsonArray(concept.Sources.Select(Source).ToArray());
        }

        front[OkfBundleKeys.Updated] = OkfTimestamps.Format(concept.Updated);
        if (links.Count > 0)
        {
            front[OkfBundleKeys.Links] = new JsonArray(links.Select(Link).ToArray());
        }

        if (facts.Count > 0)
        {
            var profile = new JsonObject();
            foreach (var fact in facts.OrderBy(f => f.Field, StringComparer.Ordinal))
            {
                profile[fact.Field] = Fact(fact);
            }

            front[OkfBundleKeys.Profile] = profile;
        }

        foreach (var (key, value) in Extra(concept.ExtraJson))
        {
            if (!OkfBundleKeys.Known.Contains(key))
            {
                front[key] = value?.DeepClone();
            }
        }

        var text = new StringBuilder("---\n");
        YamlText.Write(text, front, 0);
        text.Append("---\n");
        var body = concept.Body.Trim();
        if (body.Length > 0)
        {
            text.Append('\n').Append(body).Append('\n');
        }

        if (links.Count > 0)
        {
            // The links again as Markdown, which is how OKF itself expresses a relation.
            text.Append('\n').Append(OkfBundleKeys.RelationsHeading).Append("\n\n");
            foreach (var link in links)
            {
                text.Append("* ").Append(link.Label).Append(" [").Append(LinkText(titles.GetValueOrDefault(link.ToId, link.ToId)))
                    .Append("](/").Append(link.ToId).Append(".md)");
                if (!string.IsNullOrWhiteSpace(link.Note))
                {
                    text.Append(": ").Append(link.Note.ReplaceLineEndings(" "));
                }

                text.Append('\n');
            }
        }

        return text.ToString();
    }

    private static string Index(List<OkfConcept> concepts)
    {
        var text = new StringBuilder("---\nokf_version: \"" + OkfBundleKeys.Version + "\"\n---\n");
        foreach (var group in concepts.GroupBy(c => c.Type).OrderBy(g => Order(g.Key)).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            text.Append("\n# ").Append(Heading(group.Key)).Append("\n\n");
            foreach (var concept in group)
            {
                text.Append("* [").Append(LinkText(concept.Title ?? concept.Id)).Append("](").Append(concept.Id).Append(".md)");
                var summary = concept.Description ?? (concept.Type == OkfTypes.Observation ? FirstLine(concept.Body) : null);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    text.Append(" - ").Append(summary);
                }

                text.Append('\n');
            }
        }

        return text.ToString();
    }

    private static JsonNode? Source(OkfSource source)
    {
        var entry = new JsonObject();
        Add(entry, "id", source.Id);
        entry["resource"] = source.Resource;
        Add(entry, "title", source.Title);
        Add(entry, "author", source.Author);
        if (source.UsageCount is { } count)
        {
            entry["usage_count"] = count;
        }

        if (source.LastModified is { } modified)
        {
            entry["last_modified"] = OkfTimestamps.Format(modified);
        }

        return entry;
    }

    private static JsonNode? Link(OkfLink link)
    {
        var entry = new JsonObject
        {
            ["to"] = link.ToId,
            ["label"] = link.Label,
            ["source"] = OkfExtras.SourceName(link.Source),
            ["confidence"] = link.Confidence,
            ["updated"] = OkfTimestamps.Format(link.Updated),
        };
        Add(entry, "note", link.Note);
        return entry;
    }

    private static JsonObject Fact(ContactFact fact)
    {
        var entry = new JsonObject
        {
            ["value"] = Json(fact.ValueJson),
            ["source"] = OkfExtras.SourceName(fact.Source),
            ["confidence"] = fact.Confidence,
            ["updated"] = OkfTimestamps.Format(fact.Updated),
        };
        Add(entry, "evidence", fact.Evidence);
        if (fact.Sensitive)
        {
            entry["sensitive"] = true;
        }

        if (fact.Expires is { } expires)
        {
            entry["expires"] = OkfTimestamps.Format(expires);
        }

        return entry;
    }

    private static JsonObject Extra(string json) => Json(json) as JsonObject ?? [];

    private static JsonNode? Json(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }

    private static void Add(JsonObject map, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            map[key] = value;
        }
    }

    private static string LinkText(string text) => text.Replace('[', '(').Replace(']', ')').ReplaceLineEndings(" ");

    private static string FirstLine(string body)
    {
        var line = body.Trim().Split('\n')[0].Trim();
        return line.Length <= SummaryLength ? line : line[..SummaryLength].TrimEnd() + "...";
    }

    private static int Order(string type) => type switch
    {
        OkfTypes.Self => 0,
        OkfTypes.Contact => 1,
        OkfTypes.Concept => 2,
        OkfTypes.Observation => 4,
        _ => 3,
    };

    private static string Heading(string type) => type switch
    {
        OkfTypes.Self => "The user",
        OkfTypes.Contact => "Contacts",
        OkfTypes.Concept => "Topics",
        OkfTypes.Observation => "Observations",
        _ => type,
    };
}
