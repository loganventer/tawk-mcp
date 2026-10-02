using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

public sealed partial class OkfBundleReader : IOkfBundleReader
{
    private const string ForeignLabel = "related to";

    public OkfBundleReading Read(IReadOnlyList<OkfBundleFile> files, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(files);
        var concepts = new List<OkfConcept>();
        var links = new List<OkfLink>();
        var facts = new List<ContactFact>();
        var problems = new List<string>();
        foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var name = file.Path[(file.Path.LastIndexOf('/') + 1)..];
            if (!file.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                || name.Equals(OkfBundleKeys.IndexFile, StringComparison.OrdinalIgnoreCase)
                || name.Equals(OkfBundleKeys.LogFile, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = file.Path[..^3];
            if (!OkfIds.IsPortable(id))
            {
                problems.Add($"{file.Path}: the path has characters a concept id cannot hold.");
                continue;
            }

            var (front, body) = Split(file.Content);
            if (front is null)
            {
                problems.Add($"{file.Path}: no frontmatter block that parses.");
                continue;
            }

            var type = Text(front[OkfBundleKeys.Type]);
            if (string.IsNullOrWhiteSpace(type))
            {
                problems.Add($"{file.Path}: the frontmatter has no type.");
                continue;
            }

            var concept = Concept(id, type.Trim(), front, now);
            if (front[OkfBundleKeys.Links] is JsonArray listed)
            {
                links.AddRange(listed.OfType<JsonObject>().Select(l => Link(id, l, concept.Updated)).OfType<OkfLink>());
                body = WithoutRelations(body);
            }
            else
            {
                links.AddRange(MarkdownLinks(id, body, concept.Updated));
            }

            if (OkfIds.JidOf(id) is { } jid && front[OkfBundleKeys.Profile] is JsonObject profile)
            {
                facts.AddRange(profile.Where(p => p.Value is JsonObject).Select(p => Fact(jid, p.Key, (JsonObject)p.Value!, concept.Updated)));
            }

            concepts.Add(concept with { Body = body.Trim() });
        }

        return new OkfBundleReading(new OkfBundle(concepts, links, facts), problems);
    }

    private static (JsonObject? Front, string Body) Split(string content)
    {
        var text = content.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (null, text);
        }

        var end = text.IndexOf("\n---\n", 3, StringComparison.Ordinal);
        var tail = end < 0 && text.EndsWith("\n---", StringComparison.Ordinal) ? text.Length - 4 : end;
        if (tail < 0)
        {
            return (null, text);
        }

        var body = tail + 5 <= text.Length ? text[(tail + 5)..] : string.Empty;
        return (FrontmatterYaml.Parse(text[4..(tail + 1)]), body);
    }

    private static OkfConcept Concept(string id, string type, JsonObject front, DateTimeOffset now)
    {
        var generated = front[OkfBundleKeys.Generated] as JsonObject;
        var by = Text(generated?["by"]);
        var at = OkfTimestamps.Parse(Text(generated?["at"])) ?? now;
        var extra = new JsonObject();
        foreach (var (key, value) in front)
        {
            if (!OkfBundleKeys.Known.Contains(key))
            {
                extra[key] = value?.DeepClone();
            }
        }

        return new OkfConcept(
            id,
            type,
            NullIfBlank(Text(front[OkfBundleKeys.Title])),
            NullIfBlank(Text(front[OkfBundleKeys.Description])),
            NullIfBlank(Text(front[OkfBundleKeys.Resource])),
            Tags(front[OkfBundleKeys.Tags]),
            string.IsNullOrWhiteSpace(by) ? OkfActors.Import : by.Trim(),
            at,
            Verified(front[OkfBundleKeys.Verified]),
            Text(front[OkfBundleKeys.Status])?.Trim().ToUpperInvariant() switch { "DRAFT" => OkfStatus.Draft, "DEPRECATED" => OkfStatus.Deprecated, _ => OkfStatus.Stable },
            OkfTimestamps.Parse(Text(front[OkfBundleKeys.StaleAfter])),
            Sources(front[OkfBundleKeys.Sources]),
            extra.ToJsonString(),
            string.Empty,
            OkfTimestamps.Parse(Text(front[OkfBundleKeys.Updated])) ?? at);
    }

    private static List<string> Tags(JsonNode? node) => node switch
    {
        JsonArray list => list.Select(Text).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()).ToList(),
        JsonValue => Text(node) is { Length: > 0 } one ? [one.Trim()] : [],
        _ => [],
    };

    // A single verifier may be written as one mapping without the list dash.
    private static List<OkfVerification> Verified(JsonNode? node)
    {
        var entries = node switch { JsonArray list => list.OfType<JsonObject>(), JsonObject one => [one], _ => [] };
        return entries
            .Select(e => (By: Text(e["by"]), At: OkfTimestamps.Parse(Text(e["at"]))))
            .Where(e => !string.IsNullOrWhiteSpace(e.By) && e.At is not null)
            .Select(e => new OkfVerification(e.By!.Trim(), e.At!.Value))
            .ToList();
    }

    private static List<OkfSource> Sources(JsonNode? node) =>
        node is not JsonArray list
            ? []
            : list.OfType<JsonObject>()
                .Where(s => !string.IsNullOrWhiteSpace(Text(s["resource"])))
                .Select(s => new OkfSource(
                    Text(s["resource"])!,
                    NullIfBlank(Text(s["id"])),
                    NullIfBlank(Text(s["title"])),
                    NullIfBlank(Text(s["author"])),
                    s["usage_count"] is JsonValue count && count.TryGetValue<long>(out var n) ? n : null,
                    OkfTimestamps.Parse(Text(s["last_modified"]))))
                .ToList();

    private static OkfLink? Link(string from, JsonObject entry, DateTimeOffset fallback)
    {
        var to = Text(entry["to"]);
        var label = Text(entry["label"]);
        return string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(label)
            ? null
            : new OkfLink(
                from, to.Trim(), label.Trim(), NullIfBlank(Text(entry["note"])), Source(entry["source"]), Number(entry["confidence"]),
                OkfTimestamps.Parse(Text(entry["updated"])) ?? fallback);
    }

    private static ContactFact Fact(string jid, string field, JsonObject entry, DateTimeOffset fallback) => new(
        jid,
        field,
        (entry["value"] ?? JsonValue.Create(string.Empty)).ToJsonString(),
        Source(entry["source"]),
        Number(entry["confidence"]),
        NullIfBlank(Text(entry["evidence"])),
        entry["sensitive"] is JsonValue flag && flag.GetValueKind() == JsonValueKind.True,
        OkfTimestamps.Parse(Text(entry["updated"])) ?? fallback,
        OkfTimestamps.Parse(Text(entry["expires"])));

    // In a bundle from elsewhere a relation is any Markdown link to another concept; its kind is in the prose.
    private static IEnumerable<OkfLink> MarkdownLinks(string from, string body, DateTimeOffset updated)
    {
        var folder = from.Contains('/', StringComparison.Ordinal) ? from[..from.LastIndexOf('/')] : string.Empty;
        return MarkdownLink().Matches(body)
            .Select(m => Target(folder, m.Groups[1].Value))
            .Where(to => to is not null && to != from)
            .Distinct(StringComparer.Ordinal)
            .Select(to => new OkfLink(from, to!, ForeignLabel, null, FactSource.Imported, 1.0, updated));
    }

    private static string? Target(string folder, string target)
    {
        var path = Uri.UnescapeDataString(target.Split('#')[0]);
        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = new List<string>(path.StartsWith('/') || folder.Length == 0 ? [] : folder.Split('/'));
        foreach (var part in path.TrimStart('/').Split('/'))
        {
            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
            }
            else if (part != "." && part.Length > 0)
            {
                parts.Add(part);
            }
        }

        var id = string.Join('/', parts)[..^3];
        return OkfIds.IsPortable(id) ? id : null;
    }

    private static string WithoutRelations(string body)
    {
        var at = body.LastIndexOf("\n" + OkfBundleKeys.RelationsHeading + "\n", StringComparison.Ordinal);
        return at >= 0 ? body[..at] : body.StartsWith(OkfBundleKeys.RelationsHeading + "\n", StringComparison.Ordinal) ? string.Empty : body;
    }

    private static FactSource Source(JsonNode? node) =>
        Enum.TryParse<FactSource>(Text(node), true, out var source) && Enum.IsDefined(source) ? source : FactSource.Imported;

    private static double Number(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number
        && double.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, 0, 1)
            : 1.0;

    private static string? Text(JsonNode? node) => node is not JsonValue value
        ? null
        : value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [GeneratedRegex(@"\[[^\]]*\]\(([^)\s]+)\)")]
    private static partial Regex MarkdownLink();
}
