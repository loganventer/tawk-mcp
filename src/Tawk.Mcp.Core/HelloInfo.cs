namespace Tawk.Mcp.Core;

public sealed record HelloInfo(int Protocol, string Tawk, string Access, AccountInfo? Account, bool Connected)
{
    /// <summary>True when this tawk serves each request from the account it names. An older tawk ignores the name.</summary>
    public bool MultiAccount { get; init; }

    /// <summary>The accounts agents may use, or null from a tawk with one account.</summary>
    public IReadOnlyList<AccountSummary>? Accounts { get; init; }

    /// <summary>The id of the account a request is served by when it names none.</summary>
    public int? DefaultAccount { get; init; }

    /// <summary>What this tawk can do beyond the first shape of the protocol, by name. Null from a tawk that sends no such list.</summary>
    public IReadOnlyList<string>? Features { get; init; }

    /// <summary>Whether this tawk named <paramref name="feature"/> in its hello.</summary>
    public bool Has(string feature) => Features is not null && Features.Contains(feature, StringComparer.Ordinal);
}
