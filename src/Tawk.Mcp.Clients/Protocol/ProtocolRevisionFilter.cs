using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Tawk.Mcp.Clients.Protocol;

/// <summary>
/// Keeps clients on the initialize handshake, so they settle on revision 2025-11-25 or older.
/// Revision 2026-07-28 starts with server/discover and replaces resources/subscribe with
/// subscriptions/listen, and Claude Code does not register a channel server that negotiates it.
/// Refusing server/discover as unknown makes a client fall back to initialize.
/// </summary>
public static class ProtocolRevisionFilter
{
    public const string DiscoverMethod = "server/discover";
    public const int MethodNotFound = -32601;

    public static McpMessageFilter Filter() =>
        next => async (context, cancellationToken) =>
        {
            if (context.JsonRpcMessage is JsonRpcRequest { Method: DiscoverMethod } request)
            {
                await context.Server.SendMessageAsync(
                    new JsonRpcError
                    {
                        Id = request.Id,
                        Error = new JsonRpcErrorDetail { Code = MethodNotFound, Message = $"Method '{DiscoverMethod}' is not supported; use initialize." },
                    },
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            await next(context, cancellationToken).ConfigureAwait(false);
        };
}
