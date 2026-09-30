using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Tawk.Mcp.Clients.Resources;
using Tawk.Mcp.Core;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class ResourceUpdatePumpTests
{
    private readonly ResourceSubscriptionRegistry _registry = new();
    private readonly FakeClientSession _session = new();
    private ResourceUpdatePump _pump = null!;

    [SetUp]
    public void SetUp() => _pump = new ResourceUpdatePump(_registry, NullLogger<ResourceUpdatePump>.Instance);

    [Test]
    public async Task A_new_message_updates_the_subscribed_chat()
    {
        _registry.Subscribe(ResourceUris.Chat(Samples.MomJid), _session);

        await _pump.OnUpdateAsync(new LiveUpdate(Events.Message()), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_session.Sent.Single().Method, Is.EqualTo(NotificationMethods.ResourceUpdatedNotification));
            Assert.That((string?)_session.Sent.Single().Parameters["uri"], Is.EqualTo("tawk://chat/27820000000@s.whatsapp.net"));
        });
    }

    [Test]
    public async Task An_unread_change_updates_the_chat_list()
    {
        _registry.Subscribe(ResourceUris.Chats, _session);

        await _pump.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);

        Assert.That((string?)_session.Sent.Single().Parameters["uri"], Is.EqualTo(ResourceUris.Chats));
    }

    [Test]
    public async Task After_a_reconnect_every_subscription_is_refreshed_and_the_list_changed()
    {
        _registry.Subscribe(ResourceUris.Chats, _session);
        _registry.Subscribe(ResourceUris.Chat(Samples.MomJid), _session);

        await _pump.OnUpdateAsync(new LiveUpdate(new ConnectionStateEvent(TawkConnectionState.Connected)), CancellationToken.None);

        Assert.That(_session.Sent.Select(s => s.Method), Is.EqualTo(new[]
        {
            NotificationMethods.ResourceUpdatedNotification,
            NotificationMethods.ResourceUpdatedNotification,
            NotificationMethods.ResourceListChangedNotification,
        }));
    }

    [Test]
    public async Task A_session_that_has_gone_is_forgotten()
    {
        _session.Broken = true;
        _registry.Subscribe(ResourceUris.Chats, _session);

        await _pump.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);

        Assert.That(_registry.All(), Is.Empty);
    }

    [Test]
    public async Task Other_chats_do_not_notify()
    {
        _registry.Subscribe(ResourceUris.Chat("other@s.whatsapp.net"), _session);

        await _pump.OnUpdateAsync(new LiveUpdate(Events.Message()), CancellationToken.None);

        Assert.That(_session.Sent, Is.Empty);
    }
}
