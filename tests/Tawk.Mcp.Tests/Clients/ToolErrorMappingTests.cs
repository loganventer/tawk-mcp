using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Core;
using Tawk.Mcp.Tests.Fakes;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Clients;

public class ToolErrorMappingTests
{
    private static async Task<string> SendFailingWith(ControlError error)
    {
        var parts = new TestParts();
        parts.Control.Fail("send_message", error);
        var result = await new MessageTools(parts.Sending, parts.Messages, new AmbientAccountScope()).SendMessageAsync("Mom", "hi");
        Assert.That(result.IsError, Is.True);
        return ToolOutput.Text(result);
    }

    [TestCase("declined", "declined this in tawk")]
    [TestCase("timed_out", "within 2 minutes")]
    [TestCase("not_allowed", "access = send")]
    [TestCase("offline", "cannot do this right now")]
    [TestCase("not_found", "Locked and hidden chats are never shown")]
    [TestCase("failed", "tried and could not do it")]
    [TestCase("draft_exists", "already has a draft")]
    [TestCase("bad_token", "expired or was already used")]
    [TestCase("unsupported", "cannot do this")]
    [TestCase("bad_request", "rejected the request")]
    public async Task Each_error_code_becomes_a_clear_tool_error(string code, string expected)
    {
        Assert.That(await SendFailingWith(new ControlError(code, "from tawk")), Does.Contain(expected));
    }

    [Test]
    public async Task Ambiguous_lists_the_candidates_to_choose_from()
    {
        var text = await SendFailingWith(new ControlError(
            "ambiguous", "2 match", [new ChatRef("a@s.whatsapp.net", "Mom"), new ChatRef("b@g.us", "Mom's\n<group>")]));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("- Mom <a@s.whatsapp.net>"));
            Assert.That(text, Does.Contain("- Mom's group <b@g.us>"));
        });
    }

    [Test]
    public async Task Rate_limited_says_how_long_to_wait()
    {
        Assert.That(await SendFailingWith(new ControlError("rate_limited", "slow down", RetryAfter: 17)), Does.Contain("17 seconds"));
    }

    [Test]
    public async Task Not_running_passes_the_clear_message_through()
    {
        var parts = new TestParts();
        parts.Control.Fail("list_chats", TawkControlException.NotRunning("/run/user/1000/tawk/control.sock"));

        var result = await new ChatTools(parts.Reading, parts.Chats, new AmbientAccountScope()).ListChatsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(ToolOutput.Text(result), Does.Contain("tawk-mcp will connect as soon as it is").And.Contain("/run/user/1000/tawk/control.sock"));
        });
    }
}
