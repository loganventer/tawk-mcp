using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class ConfirmationTests
{
    private const string Token = "c1f0secret7777";
    private const string Summary = "Delete the chat with Mom here and on your phone";

    private TestParts _parts = null!;

    [SetUp]
    public void SetUp()
    {
        _parts = new TestParts();
        _parts.Control.Answer("delete_chat", $$"""{"needs_confirmation":true,"token":"{{Token}}","summary":"{{Summary}}","expires_at":1790000300}""");
    }

    private Task<string> DeleteWith(ConfirmationAnswer? answer) =>
        _parts.Chats.DeleteChatAsync(
            "Mom",
            new WriteContext(answer is { } a ? new FakeUserConfirmation(a) : null, null),
            CancellationToken.None);

    [Test]
    public async Task Accepting_confirms_with_the_token_and_reports_done()
    {
        var text = await DeleteWith(ConfirmationAnswer.Accepted);

        Assert.Multiple(() =>
        {
            Assert.That((string?)_parts.Control.Last("confirm").Args!["token"], Is.EqualTo(Token));
            Assert.That(_parts.Control.Requests.Any(r => r.Op == "cancel_confirmation"), Is.False);
            Assert.That(text, Does.Contain(Summary));
        });
    }

    [TestCase(ConfirmationAnswer.Declined)]
    [TestCase(ConfirmationAnswer.Cancelled)]
    public async Task Declining_cancels_and_says_you_declined(ConfirmationAnswer answer)
    {
        var text = await DeleteWith(answer);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("You declined"));
            Assert.That((string?)_parts.Control.Last("cancel_confirmation").Args!["token"], Is.EqualTo(Token));
            Assert.That(_parts.Control.Requests.Any(r => r.Op == "confirm"), Is.False);
        });
    }

    [Test]
    public async Task A_client_that_cannot_ask_is_refused_and_the_request_cancelled()
    {
        var result = await new ChatTools(_parts.Reading, _parts.Chats).DeleteChatAsync(null, "Mom");

        Assert.Multiple(() =>
        {
            Assert.That(ToolOutput.Text(result), Is.EqualTo("This needs your confirmation, and your MCP client cannot ask you. Do it in tawk instead."));
            Assert.That(_parts.Control.Requests.Select(r => r.Op), Is.EqualTo(new[] { "delete_chat", "cancel_confirmation" }));
        });
    }

    [Test]
    public async Task Accepting_and_then_declining_in_tawk_reports_declined()
    {
        _parts.Control.Fail("confirm", new ControlError("declined", "You declined"));

        var error = Assert.ThrowsAsync<TawkControlException>(() => DeleteWith(ConfirmationAnswer.Accepted))!;

        Assert.That(error.Code, Is.EqualTo(ControlErrorCode.Declined));
        var result = await new ChatTools(_parts.Reading, _parts.Chats).DeleteChatAsync(null, "Mom");
        Assert.That(result.IsError, Is.False);
    }

    [Test]
    public void An_expired_token_reports_bad_token_without_the_token()
    {
        _parts.Control.Fail("confirm", new ControlError("bad_token", $"Token {Token} expired"));

        var error = Assert.ThrowsAsync<TawkControlException>(() => DeleteWith(ConfirmationAnswer.Accepted))!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(ControlErrorCode.BadToken));
            Assert.That(error.Message, Does.Not.Contain(Token));
        });
    }

    [TestCase(ConfirmationAnswer.Accepted)]
    [TestCase(ConfirmationAnswer.Declined)]
    [TestCase(ConfirmationAnswer.NotSupported)]
    public async Task The_token_never_reaches_the_model_logs_or_notifications(ConfirmationAnswer answer)
    {
        var logger = new CapturingLogger<LiveUpdatesManager>();
        var session = new FakeClientSession();
        var sink = new RecordingSink();
        var live = new LiveUpdatesManager(_parts.Control, [sink], _parts.Transcript, new Tawk.Mcp.Engines.NotificationFormatter(), _parts.Fence, logger);
        using var cts = new CancellationTokenSource();
        var running = live.RunAsync(cts.Token);
        var confirmation = new FakeUserConfirmation(answer);
        var tools = new ChatTools(_parts.Reading, _parts.Chats);

        var outputs = new List<string>
        {
            await _parts.Chats.DeleteChatAsync("Mom", new WriteContext(confirmation, null), CancellationToken.None),
            ToolOutput.Text(await tools.DeleteChatAsync(null, "Mom")),
            ToolOutput.Text(await tools.ClearChatAsync(null, "Mom")),
        };
        _parts.Control.Answer("clear_chat", $$"""{"needs_confirmation":true,"token":"{{Token}}","summary":"x"}""");
        outputs.Add(ToolOutput.Text(await tools.ClearChatAsync(null, "Mom")));
        await cts.CancelAsync();
        try
        {
            await running;
        }
        catch (OperationCanceledException)
        {
        }

        var everything = string.Join('\n', outputs.Concat(logger.Lines).Concat(confirmation.Asked)
            .Concat(session.Sent.Select(s => s.Parameters.ToJsonString()))
            .Concat(sink.Updates.Select(u => JsonSerializer.Serialize(u.Event) + u.ModelText)));
        Assert.That(everything, Does.Not.Contain(Token).And.Not.Contain("c1f0"));
    }
}
