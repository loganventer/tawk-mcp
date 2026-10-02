using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Core.Okf;

/// <summary>
/// Actors in OKF's convention: producer/version for tools, human:id for people, process:id for automation.
/// </summary>
public static class OkfActors
{
    public const string Self = "human:self";
    public const string Import = "process:import";
    public const string HumanPrefix = "human:";
    public const string ProcessPrefix = "process:";

    public static string Tool(string version) => "tawk-mcp/" + version;

    public static string Human(string id) => HumanPrefix + id;

    public static bool IsHuman(string actor) => actor is not null && actor.StartsWith(HumanPrefix, StringComparison.Ordinal);

    /// <summary>How much weight an actor's word carries, in memory's terms.</summary>
    public static FactSource SourceOf(string actor) =>
        actor == Self ? FactSource.User
        : IsHuman(actor) ? FactSource.Contact
        : actor is not null && actor.StartsWith(ProcessPrefix, StringComparison.Ordinal) ? FactSource.Imported
        : FactSource.Inferred;

    /// <summary>Who wrote something that memory attributes to <paramref name="source"/>.</summary>
    public static string For(FactSource source, string? contactJid, string version) => source switch
    {
        FactSource.User => Self,
        FactSource.Contact => contactJid is null ? Human("contact") : Human(contactJid),
        FactSource.Imported => Import,
        _ => Tool(version),
    };
}
