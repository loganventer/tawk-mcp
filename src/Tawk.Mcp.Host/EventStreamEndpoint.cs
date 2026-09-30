using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Tawk.Mcp.Clients.Streaming;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Host;

/// <summary>GET /events: new messages, unread changes and tawk's connection state as server-sent events.</summary>
public sealed class EventStreamEndpoint(IEventStreamHub hub, ILiveUpdatesManager liveUpdates, TimeSpan heartbeat)
{
    public const string Path = "/events";

    public async Task HandleAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cancellationToken = context.RequestAborted;
        var response = context.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        long? lastEventId = long.TryParse(
            context.Request.Headers["Last-Event-ID"].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            ? last
            : null;

        using var subscription = hub.Subscribe(lastEventId);
        await WriteAsync(response, ": connected\n\n", cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            response,
            $"event: tawk\ndata: {{\"state\":\"{TawkConnectionStates.ToWire(liveUpdates.ConnectionState)}\"}}\n\n",
            cancellationToken).ConfigureAwait(false);
        foreach (var item in subscription.Replay)
        {
            await WriteAsync(response, Format(item), cancellationToken).ConfigureAwait(false);
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                wait.CancelAfter(heartbeat);
                bool more;
                try
                {
                    more = await subscription.Live.WaitToReadAsync(wait.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await WriteAsync(response, ": heartbeat\n\n", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!more)
                {
                    break;
                }

                while (subscription.Live.TryRead(out var item))
                {
                    await WriteAsync(response, Format(item), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The subscriber went away.
        }
    }

    public static string Format(StreamEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return string.Create(CultureInfo.InvariantCulture, $"id: {item.Id}\nevent: {item.Name}\ndata: {item.Data}\n\n");
    }

    private static async Task WriteAsync(HttpResponse response, string text, CancellationToken cancellationToken)
    {
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
