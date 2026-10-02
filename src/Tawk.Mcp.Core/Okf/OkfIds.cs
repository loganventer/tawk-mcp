using System.Globalization;

namespace Tawk.Mcp.Core.Okf;

/// <summary>Concept ids, which in OKF are bundle paths without the .md suffix.</summary>
public static class OkfIds
{
    public const string Self = "self";
    public const string ContactPrefix = "contacts/";
    public const string ObservationPrefix = "observations/";
    public const string ConceptPrefix = "concepts/";

    /// <summary>The label of the link from an observation to what it is about.</summary>
    public const string About = "about";

    public static string Contact(string jid) => ContactPrefix + jid;

    /// <summary>The same on every machine for the same note, so a merge never duplicates it.</summary>
    public static string Observation(string subject, DateTimeOffset created) =>
        ObservationPrefix + subject + "-" + created.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    /// <summary>The id of an observation about a concept, made at a given time.</summary>
    public static string ObservationAbout(string conceptId, DateTimeOffset created)
    {
        ArgumentNullException.ThrowIfNull(conceptId);
        return Observation(JidOf(conceptId) ?? conceptId.Replace('/', '-'), created);
    }

    /// <summary>
    /// Whether an id can be a file path on every system: segments of letters, digits and . _ @ + - joined by
    /// slashes, none of them a reserved name.
    /// </summary>
    public static bool IsPortable(string conceptId)
    {
        if (string.IsNullOrEmpty(conceptId) || conceptId.Length > 200)
        {
            return false;
        }

        foreach (var segment in conceptId.Split('/'))
        {
            if (segment.Length == 0 || segment.Trim('.').Length == 0 || segment.EndsWith('.')
                || segment.Equals("index", StringComparison.OrdinalIgnoreCase) || segment.Equals("log", StringComparison.OrdinalIgnoreCase)
                || segment.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '@' or '+' or '-')))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The JID of a contact concept, or null for any other concept.</summary>
    public static string? JidOf(string conceptId) =>
        conceptId is not null && conceptId.StartsWith(ContactPrefix, StringComparison.Ordinal) ? conceptId[ContactPrefix.Length..] : null;
}
