using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Engines;

public class CatchUpPlannerTests
{
    private readonly ManualTimeProvider _clock = new();

    private UnreadSummary Summary() => new(5, 1, [Chat("old", TimeSpan.FromDays(3)), Chat("new", TimeSpan.FromMinutes(20))]);

    private ChatSummary Chat(string name, TimeSpan ago) =>
        new(name + "@s.whatsapp.net", name, false, 2, false, false, false, false, (_clock.GetUtcNow() - ago).ToUnixTimeSeconds(), "hi");

    [Test]
    public void Lists_every_unread_chat_and_says_to_read_them()
    {
        var plan = new CatchUpPlanner(_clock).Plan(Summary(), null);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Chats, Has.Count.EqualTo(2));
            Assert.That(plan.Instructions, Does.Contain("read_messages").And.Contain("5 unread").And.Contain("untrusted"));
        });
    }

    [TestCase("1h")]
    [TestCase("30m")]
    [TestCase("2026-09-30T17:00")]
    public void Since_keeps_only_recent_chats(string since)
    {
        var plan = new CatchUpPlanner(_clock).Plan(Summary(), since);

        Assert.That(plan.Chats.Select(c => c.Name), Is.EqualTo(new[] { "new" }));
    }

    [Test]
    public void Since_it_cannot_read_lists_everything_and_says_so()
    {
        var plan = new CatchUpPlanner(_clock).Plan(Summary(), "whenever");

        Assert.Multiple(() =>
        {
            Assert.That(plan.Chats, Has.Count.EqualTo(2));
            Assert.That(plan.Instructions, Does.Contain("not understood"));
        });
    }

    [Test]
    public void Nothing_unread_says_so()
    {
        var plan = new CatchUpPlanner(_clock).Plan(new UnreadSummary(0, 0, []), null);

        Assert.That(plan.Instructions, Does.Contain("nothing to catch up on"));
    }
}
