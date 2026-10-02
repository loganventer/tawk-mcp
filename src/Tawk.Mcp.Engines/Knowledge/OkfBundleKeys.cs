namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>The frontmatter keys a bundle uses: OKF's own, and the ones tawk-mcp adds as a producer.</summary>
internal static class OkfBundleKeys
{
    public const string Version = "0.2";
    public const string IndexFile = "index.md";
    public const string LogFile = "log.md";
    public const string RelationsHeading = "# Relations";

    public const string Type = "type";
    public const string Title = "title";
    public const string Description = "description";
    public const string Resource = "resource";
    public const string Tags = "tags";
    public const string Generated = "generated";
    public const string Verified = "verified";
    public const string Status = "status";
    public const string StaleAfter = "stale_after";
    public const string Sources = "sources";

    // Producer keys. OKF consumers keep keys they do not know, so these survive a round trip elsewhere.
    public const string Updated = "updated";
    public const string Links = "links";
    public const string Profile = "profile";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        Type, Title, Description, Resource, Tags, Generated, Verified, Status, StaleAfter, Sources, Updated, Links, Profile,
    };
}
