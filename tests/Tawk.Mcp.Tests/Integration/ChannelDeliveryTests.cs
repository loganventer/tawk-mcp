using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Integration;

/// <summary>A Claude Code session over a stream, as with --stdio, gets each new message as one channel event.</summary>
[CancelAfter(20000)]
public class ChannelDeliveryTests
{
    [Test]
    public async Task A_new_message_reaches_a_session_once_however_many_requests_it_has_made()
    {
        await using var harness = new McpHarness();
        await harness.StartAsync();
        await harness.Server.WaitForAsync("subscribe");

        // What a client does before a message arrives: several requests on one session.
        await harness.Client.ListToolsAsync();
        await harness.Client.ListToolsAsync();
        await harness.Client.PingAsync();

        await harness.Server.SendAsync(Samples.MessageEvent("only once please", "3EB0ONCE01"));
        await harness.WaitForNotificationAsync("notifications/claude/channel");
        await Task.Delay(500);                       // time for any duplicates to arrive

        int events;
        lock (harness.Notifications)
        {
            events = harness.Notifications.Count(n => n.Method == "notifications/claude/channel");
        }

        Assert.That(events, Is.EqualTo(1), "one channel event per WhatsApp message");
    }
}
