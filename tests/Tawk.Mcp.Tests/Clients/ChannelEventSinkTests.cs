using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Core;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class ChannelEventSinkTests
{
    private readonly ClientSessionRegistry _sessions = new();
    private readonly FakeClientSession _claude = new("claude-code");
    private readonly FakeClientSession _other = new("some-editor");

    [SetUp]
    public void SetUp()
    {
        _sessions.Add(_claude);
        _sessions.Add(_other);
    }

    private static LiveUpdate Update(bool fromMe = false) => new(Events.Message("hi", fromMe), "New WhatsApp message ...\n<<<BEGIN UNTRUSTED CHAT DATA>>>\nhi\n<<<END UNTRUSTED CHAT DATA>>>");

    [Test]
    public async Task Sends_a_channel_event_to_claude_code_with_meta()
    {
        await new ChannelEventSink(new ChannelOptions(ChannelMode.Auto), _sessions).OnUpdateAsync(Update(), CancellationToken.None);

        var (method, parameters) = _claude.Sent.Single();
        var meta = parameters["meta"]!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(method, Is.EqualTo("notifications/claude/channel"));
            Assert.That((string?)parameters["content"], Does.Contain("UNTRUSTED CHAT DATA"));
            Assert.That((string?)meta["chat_jid"], Is.EqualTo(Samples.MomJid));
            Assert.That((string?)meta["message_id"], Is.EqualTo("3EB0C2A1F0"));
            Assert.That(meta.Select(p => p.Key), Is.EquivalentTo(new[] { "chat_jid", "chat_name", "message_id", "sender", "ts", "type" }));
            Assert.That(meta.Select(p => p.Key), Has.All.Match("^[A-Za-z0-9_]+$"));
            Assert.That(_other.Sent, Is.Empty);
        });
    }

    [Test]
    public async Task Nothing_is_sent_when_the_channel_is_off()
    {
        await new ChannelEventSink(new ChannelOptions(ChannelMode.Off), _sessions).OnUpdateAsync(Update(), CancellationToken.None);

        Assert.That(_claude.Sent, Is.Empty);
    }

    [Test]
    public async Task On_sends_to_every_session()
    {
        await new ChannelEventSink(new ChannelOptions(ChannelMode.On), _sessions).OnUpdateAsync(Update(), CancellationToken.None);

        Assert.That(_other.Sent, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task The_users_own_messages_are_not_pushed()
    {
        await new ChannelEventSink(new ChannelOptions(ChannelMode.On), _sessions).OnUpdateAsync(Update(fromMe: true), CancellationToken.None);

        Assert.That(_claude.Sent, Is.Empty);
    }

    [Test]
    public async Task A_session_that_has_gone_is_dropped()
    {
        _claude.Broken = true;

        await new ChannelEventSink(new ChannelOptions(ChannelMode.Auto), _sessions).OnUpdateAsync(Update(), CancellationToken.None);

        Assert.That(_sessions.Sessions, Does.Not.Contain(_claude));
    }
}
