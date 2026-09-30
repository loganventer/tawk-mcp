using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

public interface IStatusManager
{
    Task<string> StatusViewersAsync(string statusId, CancellationToken cancellationToken);

    Task<string> ListBackgroundsAsync(CancellationToken cancellationToken);

    Task<string> PostStatusAsync(string kind, string? text, string? file, string? background, WriteContext context, CancellationToken cancellationToken);

    Task<string> ReplyStatusAsync(string statusId, string text, WriteContext context, CancellationToken cancellationToken);

    Task<string> LikeStatusAsync(string statusId, WriteContext context, CancellationToken cancellationToken);
}
