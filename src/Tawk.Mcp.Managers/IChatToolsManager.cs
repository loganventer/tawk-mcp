using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

/// <summary>
/// The tools for a long chat list that tawk keeps on the user's computer: their own labels on chats, the chats
/// they put aside with a reminder, and the chats where their last message is unanswered. Nothing here reaches WhatsApp.
/// </summary>
public interface IChatToolsManager
{
    Task<string> ListLabelsAsync(string? chat, CancellationToken cancellationToken);

    Task<string> SetLabelAsync(string chat, string label, bool carry, WriteContext context, CancellationToken cancellationToken);

    Task<string> ListRemindersAsync(CancellationToken cancellationToken);

    Task<string> SetReminderAsync(string chat, string until, WriteContext context, CancellationToken cancellationToken);

    Task<string> CancelReminderAsync(string chat, WriteContext context, CancellationToken cancellationToken);

    Task<string> AwaitingRepliesAsync(int? days, CancellationToken cancellationToken);
}
