namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>The tables that sync, parents before the rows that depend on them.</summary>
internal static class SyncTables
{
    // A concept says who it came from in its producer keys, or failing that through the actor that generated it.
    private const string ConceptSource =
        "COALESCE(json_extract(extra, '$.source'), CASE WHEN generated_by = 'human:self' THEN 'user' "
        + "WHEN generated_by LIKE 'human:%' THEN 'contact' WHEN generated_by LIKE 'process:%' THEN 'imported' ELSE 'inferred' END)";

    public static readonly IReadOnlyList<SyncTable> All =
    [
        new("category", ["path"], ["path", "description", "updated"]),
        new("voice", ["name"], ["name", "description", "guide", "rules", "is_default", "updated"]),
        new("voice_variant", ["voice", "category"], ["voice", "category", "guide", "rules", "examples", "updated"],
            ParentTable: "voice", ParentKey: "name", ParentColumn: "voice"),
        new("contact", ["jid"], ["jid", "display_name", "voice", "updated"]),
        new("contact_category", ["jid", "category"], ["jid", "category", "updated"],
            ParentTable: "contact", ParentKey: "jid", ParentColumn: "jid"),
        new("contact_fact", ["jid", "field"],
            ["jid", "field", "value", "source", "confidence", "evidence", "sensitive", "updated", "expires", "account"],
            SourceSql: "source", ExpiresColumn: "expires", ParentTable: "contact", ParentKey: "jid", ParentColumn: "jid"),
        new("response_template", ["name"], ["name", "description", "category", "voice", "language", "body", "updated"]),
        new("okf_concept", ["id"],
            ["id", "type", "title", "description", "resource", "tags", "generated_by", "generated_at", "verified", "status",
             "stale_after", "sources", "extra", "body", "updated"],
            SourceSql: ConceptSource),
        new("okf_link", ["from_id", "to_id", "label"], ["from_id", "to_id", "label", "note", "source", "confidence", "updated", "account"],
            SourceSql: "source", ParentTable: "okf_concept", ParentKey: "id", ParentColumn: "from_id"),
    ];
}
