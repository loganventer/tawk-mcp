using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>Knowledge held as Open Knowledge Format concepts and the links between them.</summary>
public interface IOkfStore
{
    Task<OkfConcept?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Concepts, newest first. Each filter is optional: a type, a concept they link to, a tag, and text
    /// found in the title, description or body.
    /// </summary>
    Task<IReadOnlyList<OkfConcept>> ListAsync(string? type, string? linkedTo, string? tag, string? query, CancellationToken cancellationToken);

    Task UpsertAsync(OkfConcept concept, CancellationToken cancellationToken);

    /// <summary>Removes a concept and its outgoing links, and records the delete for the memory sync.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<OkfLink>> GetLinksFromAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<OkfLink>> GetLinksToAsync(string id, CancellationToken cancellationToken);

    /// <summary>Every link there is.</summary>
    Task<IReadOnlyList<OkfLink>> ListLinksAsync(CancellationToken cancellationToken);

    Task UpsertLinkAsync(OkfLink link, CancellationToken cancellationToken);

    Task<bool> DeleteLinkAsync(string fromId, string toId, string label, CancellationToken cancellationToken);
}
