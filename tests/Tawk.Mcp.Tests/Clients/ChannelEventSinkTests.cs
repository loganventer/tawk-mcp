using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class ChannelEventSinkTests
{
    private readonly ClientSessionRegistry _sessions = new();
    private readonly FakeClientSession _claude = new("claude-code");
    private readonly FakeClientSession _other = new("some-editor");
    private readonly WorkflowCadence _cadence = new(new WorkflowOptions(2, null));

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
        await new ChannelEventSink(new ChannelOptions(ChannelMode.Auto), _sessions, _cadence, new ChannelContextHints()).OnUpdateAsync(Update(), CancellationToken.None);

        var (method, parameters) = _claude.Sent.Single();
        var meta = parameters["meta"]!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(method, Is.EqualTo("notifications/claude/channel"));
            Assert.That((string?)parameters["content"], Does.Contain("UNTRUSTED CHAT DATA"));
            Assert.That((string?)parameters["content"], Does.Contain($"call read_messages with chat \"{Samples.MomJid}\"").And.EndWith("carry on."));
            Assert.That(((string)parameters["content"]!).IndexOf("Context:", StringComparison.Ordinal),
                Is.GreaterThan(((string)parameters["content"]!).IndexOf("END UNTRUSTED CHAT DATA", StringComparison.Ordinal)));
            Assert.That((string?)meta["chat_jid"], Is.EqualTo(Samples.MomJid));
            Assert.That((string?)meta["message_id"], Is.EqualTo("3EB0C2A1F0"));
            Assert.That(meta.Select(p => p.Key), Is.EquivalentTo(new[] { "chat_jid", "chat_name", "message_id", "sender", "ts", "type", "from_me" }));
            Assert.That(meta.Select(p => p.Key), Has.All.Match("^[A-Za-z0-9_]+$"));
            Assert.That(_other.Sent, Is.Empty);
        });
    }

    [Test]
    public async Task Nothing_is_sent_when_the_channel_is_off()
    {
        await new ChannelEventSink(new ChannelOptions(ChannelMode.Off), _sessions, _cadence, new ChannelContextHints()).OnUpdateAsync(Update(), CancellationToken.None);

        Assert.That(_claude.Sent, Is.Empty);
    }

    [Test]
    public async Task On_sends_to_every_session()
    {
        await new ChannelEventSink(new ChannelOptions(ChannelMode.On), _sessions, _cadence, new ChannelContextHints()).OnUpdateAsync(Update(), CancellationToken.None);

        Assert.That(_other.Sent, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task The_users_own_messages_are_pushed_when_asked_for_and_marked()
    {
        var sink = new ChannelEventSink(new ChannelOptions(ChannelMode.On, OwnMessages: true), _sessions, _cadence, new ChannelContextHints());

        await sink.OnUpdateAsync(Update(fromMe: true), CancellationToken.None);
        await sink.OnUpdateAsync(Update(), CancellationToken.None);

        var marks = _claude.Sent.Select(sent => (string?)sent.Parameters!["meta"]!["from_me"]).ToList();
        Assert.That(marks, Is.EqualTo(new[] { "true", "false" }));
    }

    [Test]
    public async Task A_read_receipt_is_pushed_only_when_asked_for_and_is_not_a_round()
    {
        var read = new LiveUpdate(
            new ReadEvent(new ChatRef(Samples.MomJid, "Mom"), "3EB0C2A1F0", new ReaderRef(Samples.MomJid, "Mom"), 1790791400),
            "Read receipt: Mom read the user's message in \"Mom\" (id 3EB0C2A1F0).");

        await new ChannelEventSink(new ChannelOptions(ChannelMode.On), _sessions, _cadence, new ChannelContextHints()).OnUpdateAsync(read, CancellationToken.None);
        var before = _claude.Sent.Count;
        await new ChannelEventSink(new ChannelOptions(ChannelMode.On, ReadReceipts: true), _sessions, _cadence, new ChannelContextHints()).OnUpdateAsync(read, CancellationToken.None);

        var meta = _claude.Sent.Single().Parameters["meta"]!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Zero);
            Assert.That((string?)meta["type"], Is.EqualTo("read"));
            Assert.That((string?)meta["message_id"], Is.EqualTo("3EB0C2A1F0"));
            Assert.That((string?)meta["ts"], Is.EqualTo("1790791400"));
        });
    }

    [Test]
    public async Task Reactions_edits_and_scheduled_sends_each_follow_their_own_option()
    {
        var chat = new ChatRef(Samples.MomJid, "Mom");
        var mom = new ReaderRef(Samples.MomJid, "Mom");
        var updates = new[]
        {
            new LiveUpdate(new MessageActivityEvent(ActivityKind.Reaction, chat, "A1", mom, "+", null, 10), "Reaction"),
            new LiveUpdate(new MessageActivityEvent(ActivityKind.Edited, chat, "A2", mom, null, null, 11), "Message edited"),
            new LiveUpdate(new MessageActivityEvent(ActivityKind.Deleted, chat, "A3", mom, null, null, 12), "Message deleted"),
            new LiveUpdate(new MessageActivityEvent(ActivityKind.ScheduledSent, chat, "A4", null, null, null, 13), "Scheduled message sent"),
        };

        var off = new ChannelEventSink(new ChannelOptions(ChannelMode.On), _sessions, _cadence, new ChannelContextHints());
        var edits = new ChannelEventSink(new ChannelOptions(ChannelMode.On, Edits: true), _sessions, _cadence, new ChannelContextHints());
        foreach (var update in updates)
        {
            await off.OnUpdateAsync(update, CancellationToken.None);
        }

        var before = _claude.Sent.Count;
        foreach (var update in updates)
        {
            await edits.OnUpdateAsync(update, CancellationToken.None);
        }

        var all = new ChannelEventSink(new ChannelOptions(ChannelMode.On, Reactions: true, Edits: true, Scheduled: true), _sessions, _cadence, new ChannelContextHints());
        var types = _claude.Sent.Select(sent => (string?)sent.Parameters["meta"]!["type"]).ToList();
        _claude.Sent.Clear();
        foreach (var update in updates)
        {
            await all.OnUpdateAsync(update, CancellationToken.None);
        }

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Zero);
            Assert.That(types, Is.EqualTo(new[] { "edit", "delete" }));
            Assert.That(_claude.Sent.Select(sent => (string?)sent.Parameters["meta"]!["type"]), Is.EqualTo(new[] { "reaction", "edit", "delete", "scheduled_sent" }));
        });
    }

    [Test]
    public async Task Only_the_first_event_of_a_chat_points_at_its_history()
    {
        var sink = new ChannelEventSink(new ChannelOptions(ChannelMode.On), _sessions, _cadence, new ChannelContextHints());

        await sink.OnUpdateAsync(Update(), CancellationToken.None);
        await sink.OnUpdateAsync(Update(), CancellationToken.None);

        var contents = _claude.Sent.Select(sent => (string)sent.Parameters["content"]!).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(contents[0], Does.Contain("Context:"));
            Assert.That(contents[1], Does.Not.Contain("Context:"));
        });
    }

    [Test]
    public async Task The_users_own_messages_are_not_pushed()
    {
        await new ChannelEventSink(new ChannelOptions(ChannelMode.On), _sessions, _cadence, new ChannelContextHints()).OnUpdateAsync(Update(fromMe: true), CancellationToken.None);

        Assert.That(_claude.Sent, Is.Empty);
    }

    [Test]
    public async Task A_session_that_has_gone_is_dropped()
    {
        _claude.Broken = true;

        await new ChannelEventSink(new ChannelOptions(ChannelMode.Auto), _sessions, _cadence, new ChannelContextHints()).OnUpdateAsync(Update(), CancellationToken.None);

        Assert.That(_sessions.Sessions, Does.Not.Contain(_claude));
    }

    [Test]
    public void The_session_heard_from_longest_ago_is_let_go_past_the_cap()
    {
        var evicted = new List<IClientSession>();
        var registry = new ClientSessionRegistry(evicted.Add, capacity: 2);
        var third = new FakeClientSession("third");

        registry.Add(_claude);
        registry.Add(_other);
        registry.Add(_claude);
        registry.Add(third);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Sessions, Is.EqualTo(new IClientSession[] { _claude, third }));
            Assert.That(evicted, Is.EqualTo(new IClientSession[] { _other }));
        });
    }
}
