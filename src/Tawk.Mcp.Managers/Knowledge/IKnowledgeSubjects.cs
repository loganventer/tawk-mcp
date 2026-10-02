namespace Tawk.Mcp.Managers.Knowledge;

/// <summary>Works out which concept a tool argument means.</summary>
public interface IKnowledgeSubjects
{
    /// <summary>
    /// "self" is the user, an id under concepts/ is a topic, and anything else is a chat's jid or name.
    /// With <paramref name="create"/> the concept is made when it does not exist yet.
    /// </summary>
    Task<KnowledgeSubject> ResolveAsync(string subject, bool create, CancellationToken cancellationToken);
}
