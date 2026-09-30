namespace Tawk.Mcp.Managers;

/// <summary>
/// Writing use cases. tawk asks the user to approve each one; <c>onApprovalWaiting</c> runs when it starts waiting.
/// </summary>
public interface IMessageSendingManager
{
    Task<string> SendMessageAsync(string chat, string text, string? replyTo, Action? onApprovalWaiting, CancellationToken cancellationToken);

    Task<string> ReactAsync(string messageId, string emoji, Action? onApprovalWaiting, CancellationToken cancellationToken);

    Task<string> ScheduleMessageAsync(string chat, string dueAt, string text, Action? onApprovalWaiting, CancellationToken cancellationToken);

    /// <summary>Puts text into the chat's draft in tawk for the user to edit and send. Nothing is sent.</summary>
    Task<string> DraftMessageAsync(string chat, string text, CancellationToken cancellationToken);

    Task<string> MarkReadAsync(string chat, Action? onApprovalWaiting, CancellationToken cancellationToken);
}
