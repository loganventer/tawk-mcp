using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public interface IContactStore
{
    Task<ContactRecord?> GetAsync(string jid, CancellationToken cancellationToken);

    /// <summary>Contacts in a category (or any), whose name or jid contains the query (or any).</summary>
    Task<IReadOnlyList<ContactRecord>> ListAsync(string? category, string? query, CancellationToken cancellationToken);

    Task UpsertAsync(ContactRecord contact, CancellationToken cancellationToken);

    /// <summary>Removes the contact with every fact, note and category it has.</summary>
    Task<bool> DeleteAsync(string jid, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCategoriesAsync(string jid, CancellationToken cancellationToken);

    Task SetCategoriesAsync(string jid, IReadOnlyList<string> categories, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactFact>> GetFactsAsync(string jid, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactFact>> FindFactsAsync(string field, CancellationToken cancellationToken);

    Task UpsertFactAsync(ContactFact fact, CancellationToken cancellationToken);

    Task<bool> DeleteFactAsync(string jid, string field, CancellationToken cancellationToken);

    /// <summary>Removes facts whose expiry is at or before the given time and returns how many went.</summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<long> AddNoteAsync(ContactNote note, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContactNote>> GetNotesAsync(string jid, CancellationToken cancellationToken);
}
