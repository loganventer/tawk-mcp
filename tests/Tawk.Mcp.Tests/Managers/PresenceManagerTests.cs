using System.Text.Json;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Managers;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers;

public class PresenceManagerTests
{
    private readonly FakeTawkControl _control = new();
    private readonly ManualTimeProvider _clock = new();
    private FakeDelay _delay = null!;
    private PresenceManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _delay = new FakeDelay(_clock);
        _manager = new PresenceManager(_control, new PresenceFormatter(_clock), _delay, new PresenceLookupOptions(3, TimeSpan.FromMilliseconds(500)));
    }

    private static JsonElement Answer(string state, bool watching, long? lastSeen = null) => JsonDocument.Parse(
        $$"""{"chat":{{Samples.Chat}},"state":"{{state}}","watching":{{(watching ? "true" : "false")}}{{(lastSeen is { } seen ? $",\"last_seen\":{seen}" : string.Empty)}}}""").RootElement.Clone();

    [Test]
    public async Task Answers_at_once_when_tawk_already_knows()
    {
        _control.Answer("presence", _ => Answer("online", true));

        var text = await _manager.OnlineStatusAsync("Mom", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("Mom is online on WhatsApp now."));
            Assert.That((string?)_control.Last("presence").Args!["chat"], Is.EqualTo("Mom"));
            Assert.That(_control.Requests.Count(r => r.Op == "presence"), Is.EqualTo(1));
            Assert.That(_delay.Delays, Is.Empty);
        });
    }

    [Test]
    public async Task Waits_for_WhatsApps_answer_and_asks_again()
    {
        var asked = 0;
        _control.Answer("presence", _ => ++asked < 3 ? Answer("unknown", true) : Answer("offline", true, 1791363900));

        var text = await _manager.OnlineStatusAsync("Mom", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("Mom is offline. Last seen 2026-10-07 09:05."));
            Assert.That(asked, Is.EqualTo(3));
            Assert.That(_delay.Delays, Is.EqualTo(new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500) }));
        });
    }

    [Test]
    public async Task Gives_up_after_its_retries_and_says_nothing_came()
    {
        _control.Answer("presence", _ => Answer("unknown", true));

        var text = await _manager.OnlineStatusAsync("Mom", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("WhatsApp gave no answer"));
            Assert.That(_control.Requests.Count(r => r.Op == "presence"), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Does_not_wait_when_tawk_cannot_learn_anything()
    {
        _control.Answer("presence", _ => Answer("unknown", false));

        var text = await _manager.OnlineStatusAsync("Mom", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("tawk is not shown as online itself"));
            Assert.That(_control.Requests.Count(r => r.Op == "presence"), Is.EqualTo(1));
            Assert.That(_delay.Delays, Is.Empty);
        });
    }
}
