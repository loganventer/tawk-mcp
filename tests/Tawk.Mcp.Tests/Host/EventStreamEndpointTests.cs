using Microsoft.AspNetCore.Http;
using Tawk.Mcp.Clients.Streaming;
using Tawk.Mcp.Core;
using Tawk.Mcp.Host;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Host;

[CancelAfter(10000)]
public class EventStreamEndpointTests
{
    private readonly EventStreamHub _hub = new();
    private readonly FakeLiveUpdatesManager _live = new() { ConnectionState = TawkConnectionState.Waiting };

    private async Task<string> Stream(string? lastEventId, TimeSpan heartbeat, TimeSpan runFor, Func<Task>? during = null)
    {
        var body = new GrowingStream();
        using var aborted = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = body;
        if (lastEventId is not null)
        {
            context.Request.Headers["Last-Event-ID"] = lastEventId;
        }

        var handling = new EventStreamEndpoint(_hub, _live, heartbeat).HandleAsync(context);
        await Task.Delay(50);
        if (during is not null)
        {
            await during();
        }

        await Task.Delay(runFor);
        await aborted.CancelAsync();
        await handling;
        Assert.That(context.Response.ContentType, Is.EqualTo("text/event-stream"));
        return body.Text;
    }

    [Test]
    public async Task Starts_with_the_connection_state_and_then_streams_events()
    {
        var text = await Stream(null, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(200), () =>
            _hub.OnUpdateAsync(new LiveUpdate(Events.Message("hello")), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith(": connected\n\nevent: tawk\ndata: {\"state\":\"waiting\"}\n\n"));
            Assert.That(text, Does.Contain("id: 1\nevent: message\ndata: {\"chat\":{\"jid\":\"27820000000@s.whatsapp.net\""));
            Assert.That(text, Does.Contain("\"text\":\"hello\""));
        });
    }

    [Test]
    public async Task Replays_events_after_the_last_event_id()
    {
        for (var i = 0; i < 4; i++)
        {
            await _hub.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);
        }

        var text = await Stream("2", TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(100));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Not.Contain("id: 2\n"));
            Assert.That(text, Does.Contain("id: 3\nevent: chat"));
            Assert.That(text, Does.Contain("id: 4\nevent: chat"));
        });
    }

    [Test]
    public async Task Sends_a_heartbeat_comment_when_quiet()
    {
        var text = await Stream(null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));

        Assert.That(text, Does.Contain(": heartbeat\n\n"));
    }
}
