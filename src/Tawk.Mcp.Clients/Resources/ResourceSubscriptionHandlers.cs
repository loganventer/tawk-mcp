using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Clients.Sessions;

namespace Tawk.Mcp.Clients.Resources;

/// <summary>The SDK's subscribe and unsubscribe handlers. tawk-mcp already follows every chat, so only the registry changes.</summary>
public static class ResourceSubscriptionHandlers
{
    public static ValueTask<EmptyResult> SubscribeAsync(RequestContext<SubscribeRequestParams> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var uri = context.Params?.Uri ?? throw new McpException("A resource uri is required.");
        try
        {
            Registry(context).Subscribe(uri, new McpClientSession(context.Server));
        }
        catch (ArgumentException ex)
        {
            throw new McpException(ex.Message, ex);
        }

        return ValueTask.FromResult(new EmptyResult());
    }

    public static ValueTask<EmptyResult> UnsubscribeAsync(RequestContext<UnsubscribeRequestParams> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Params?.Uri is { } uri)
        {
            Registry(context).Unsubscribe(uri, new McpClientSession(context.Server));
        }

        return ValueTask.FromResult(new EmptyResult());
    }

    private static IResourceSubscriptionRegistry Registry(MessageContext context) =>
        (context.Services ?? context.Server.Services)!.GetRequiredService<IResourceSubscriptionRegistry>();
}
