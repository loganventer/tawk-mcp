using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

public interface IMessageManagementManager
{
    Task<string> EditMessageAsync(string messageId, string text, WriteContext context, CancellationToken cancellationToken);

    Task<string> DeleteMessageAsync(string messageId, bool forEveryone, WriteContext context, CancellationToken cancellationToken);

    Task<string> ForwardMessageAsync(string messageId, IReadOnlyList<string> chats, WriteContext context, CancellationToken cancellationToken);

    Task<string> RetryMessageAsync(string messageId, WriteContext context, CancellationToken cancellationToken);

    Task<string> DownloadMediaAsync(string messageId, WriteContext context, CancellationToken cancellationToken);
}
