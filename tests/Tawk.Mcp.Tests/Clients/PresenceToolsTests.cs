using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Managers;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class PresenceToolsTests
{
    private readonly FakeTawkControl _control = new();
    private PresenceTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        var clock = new ManualTimeProvider();
        _tools = new PresenceTools(
            new PresenceManager(_control, new PresenceFormatter(clock), new FakeDelay(clock), new PresenceLookupOptions()),
            new AmbientAccountScope());
    }

    [Test]
    public async Task Get_online_status_names_the_chat_and_says_what_tawk_knows()
    {
        _control.Answer("presence", $$"""{"chat":{{Samples.Chat}},"state":"offline","last_seen":1791363900,"watching":true}""");

        var result = await _tools.GetOnlineStatusAsync("Mom");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That((string?)_control.Last("presence").Args!["chat"], Is.EqualTo("Mom"));
            Assert.That(ToolOutput.Text(result), Is.EqualTo("Mom is offline. Last seen 2026-10-07 09:05."));
        });
    }

    [Test]
    public async Task Get_online_status_says_so_when_the_user_has_not_switched_it_on()
    {
        _control.Fail("presence", new ControlError("not_allowed", "Looking up who is online is switched off in tawk (Settings, Automation, Look up online status)"));

        var result = await _tools.GetOnlineStatusAsync("Mom");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(ToolOutput.Text(result), Does.Contain("switched off in tawk"));
        });
    }
}
