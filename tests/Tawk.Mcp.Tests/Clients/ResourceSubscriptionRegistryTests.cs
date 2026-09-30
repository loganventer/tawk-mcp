using Tawk.Mcp.Clients.Resources;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class ResourceSubscriptionRegistryTests
{
    private readonly ResourceSubscriptionRegistry _registry = new();
    private readonly FakeClientSession _one = new();
    private readonly FakeClientSession _two = new();

    [Test]
    public void A_chat_change_reaches_that_chat_and_the_chat_list()
    {
        _registry.Subscribe(ResourceUris.Chats, _one);
        _registry.Subscribe(ResourceUris.Chat(Samples.MomJid), _two);
        _registry.Subscribe(ResourceUris.Chat("other@s.whatsapp.net"), _two);

        var targets = _registry.TargetsForChat(Samples.MomJid);

        Assert.That(targets, Is.EquivalentTo(new[]
        {
            new ResourceTarget(_one, ResourceUris.Chats),
            new ResourceTarget(_two, ResourceUris.Chat(Samples.MomJid)),
        }));
    }

    [Test]
    public void Escaped_uris_match_their_chat_and_keep_the_uri_the_client_used()
    {
        const string escaped = "tawk://chat/27820000000%40s.whatsapp.net";
        _registry.Subscribe(escaped, _one);

        Assert.That(_registry.TargetsForChat(Samples.MomJid).Single().Uri, Is.EqualTo(escaped));
    }

    [Test]
    public void Unsubscribe_and_removing_a_session_stop_updates()
    {
        _registry.Subscribe(ResourceUris.Chats, _one);
        _registry.Subscribe(ResourceUris.Chats, _two);

        _registry.Unsubscribe(ResourceUris.Chats, _one);
        _registry.RemoveSession(_two);

        Assert.That(_registry.All(), Is.Empty);
    }

    [Test]
    public void Unknown_uris_are_refused()
    {
        Assert.Throws<ArgumentException>(() => _registry.Subscribe("tawk://secrets", _one));
    }
}
