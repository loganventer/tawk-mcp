namespace Tawk.Mcp.Managers;

/// <summary>Reading use cases. Every result is model-ready text with chat content fenced as untrusted.</summary>
public interface IChatReadingManager
{
    Task<string> ListChatsAsync(string? filter, bool unreadOnly, int? limit, CancellationToken cancellationToken);

    Task<string> ReadMessagesAsync(string chat, long? before, int? limit, CancellationToken cancellationToken);

    Task<string> SearchMessagesAsync(string query, string? chat, int? limit, CancellationToken cancellationToken);

    Task<string> UnreadSummaryAsync(CancellationToken cancellationToken);

    Task<string> ChatInfoAsync(string chat, CancellationToken cancellationToken);

    Task<string> ListStatusesAsync(bool includeArchived, CancellationToken cancellationToken);

    Task<string> ListScheduledAsync(string? chat, CancellationToken cancellationToken);

    Task<string> CatchUpPromptAsync(string? since, CancellationToken cancellationToken);

    Task<string> DraftReplyPromptAsync(string chat, CancellationToken cancellationToken);
}
