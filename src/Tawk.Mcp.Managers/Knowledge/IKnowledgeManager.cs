namespace Tawk.Mcp.Managers.Knowledge;

/// <summary>Knowledge about people and topics: observations, and the relations between concepts.</summary>
public interface IKnowledgeManager
{
    Task<string> RecordObservationAsync(
        string about, string text, string source, IReadOnlyList<string>? tags, double? confidence, bool sensitive, string? evidence,
        int? staleAfterDays, CancellationToken cancellationToken);

    Task<string> ListObservationsAsync(string? about, string? tag, string? query, bool includeSensitive, CancellationToken cancellationToken);

    Task<string> RecordRelationAsync(string first, string label, string second, string source, double? confidence, string? note, CancellationToken cancellationToken);

    Task<string> GetKnowledgeAsync(string subject, bool includeSensitive, CancellationToken cancellationToken);

    Task<string> ForgetObservationAsync(string id, CancellationToken cancellationToken);

    Task<string> ForgetRelationAsync(string first, string label, string second, CancellationToken cancellationToken);
}
