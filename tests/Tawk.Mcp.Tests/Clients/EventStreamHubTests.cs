using System.Text.Json.Nodes;
using Tawk.Mcp.Clients.Streaming;
using Tawk.Mcp.Core;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class EventStreamHubTests
{
    [Test]
    public async Task Turns_updates_into_named_events_with_json()
    {
        var hub = new EventStreamHub();
        using var subscription = hub.Subscribe(null);

        await hub.OnUpdateAsync(new LiveUpdate(Events.Message("hi")), CancellationToken.None);
        await hub.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);
        await hub.OnUpdateAsync(new LiveUpdate(new ConnectionStateEvent(TawkConnectionState.CircuitOpen)), CancellationToken.None);

        subscription.Live.TryRead(out var message);
        subscription.Live.TryRead(out var chat);
        subscription.Live.TryRead(out var state);
        Assert.Multiple(() =>
        {
            Assert.That(message!.Name, Is.EqualTo("message"));
            Assert.That((string?)JsonNode.Parse(message.Data)!["message"]!["sender_name"], Is.EqualTo("Mom"));
            Assert.That((string?)JsonNode.Parse(message.Data)!["chat"]!["jid"], Is.EqualTo(Samples.MomJid));
            Assert.That(chat!.Name, Is.EqualTo("chat"));
            Assert.That(state!.Data, Is.EqualTo("""{"state":"circuit_open"}"""));
            Assert.That(new[] { message.Id, chat.Id, state.Id }, Is.EqualTo(new long[] { 1, 2, 3 }));
        });
    }

    [Test]
    public async Task Replays_what_came_after_the_last_event_id()
    {
        var hub = new EventStreamHub();
        for (var i = 0; i < 5; i++)
        {
            await hub.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);
        }

        using var subscription = hub.Subscribe(3);

        Assert.That(subscription.Replay.Select(e => e.Id), Is.EqualTo(new long[] { 4, 5 }));
    }

    [Test]
    public async Task Keeps_only_the_last_200_events()
    {
        var hub = new EventStreamHub();
        for (var i = 0; i < 250; i++)
        {
            await hub.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);
        }

        using var subscription = hub.Subscribe(0);

        Assert.Multiple(() =>
        {
            Assert.That(subscription.Replay, Has.Count.EqualTo(200));
            Assert.That(subscription.Replay[0].Id, Is.EqualTo(51));
        });
    }

    [Test]
    public void A_new_subscriber_without_an_id_gets_no_replay()
    {
        Assert.That(new EventStreamHub().Subscribe(null).Replay, Is.Empty);
    }
}
