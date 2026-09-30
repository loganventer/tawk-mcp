using Tawk.Mcp.Clients;
using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class MessageToolsTests
{
    private TestParts _parts = null!;
    private MessageTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _parts = new TestParts();
        _tools = new MessageTools(_parts.Sending, _parts.Messages);
    }

    [Test]
    public async Task Send_message_reports_waiting_for_approval_as_progress()
    {
        _parts.Control.Answer("send_message", """{"id":"3EB0D41C22"}""");
        _parts.Control.AskForApproval.Add("send_message");
        var progress = new FakeProgress();

        var result = await _tools.SendMessageAsync("Mom", "On my way", "3EB0AA", progress);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That(ToolOutput.Text(result), Does.Contain("3EB0D41C22"));
            Assert.That(progress.Reports.Single().Message, Is.EqualTo(ApprovalProgress.WaitingMessage));
            Assert.That((string?)_parts.Control.Last("send_message").Args!["reply_to"], Is.EqualTo("3EB0AA"));
        });
    }

    [Test]
    public async Task Send_message_shows_the_final_wording_when_the_user_edited_it()
    {
        _parts.Control.Answer("send_message", """{"id":"3EB0","edited":true,"text":"On my way, 10 minutes"}""");

        var text = ToolOutput.Text(await _tools.SendMessageAsync("Mom", "On my way"));

        Assert.That(text, Does.StartWith("Sent after you edited it in tawk").And.EndWith("On my way, 10 minutes"));
    }

    [Test]
    public async Task Draft_message_says_nothing_was_sent()
    {
        _parts.Control.Answer("draft_message", """{"drafted":true}""");

        var text = ToolOutput.Text(await _tools.DraftMessageAsync("Mom", "Shall I bring bread?"));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("nothing has been sent"));
            Assert.That((string?)_parts.Control.Last("draft_message").Args!["text"], Is.EqualTo("Shall I bring bread?"));
        });
    }

    [Test]
    public async Task React_and_mark_read_call_their_operations()
    {
        var react = ToolOutput.Text(await _tools.ReactAsync("3EB0", "👍"));
        var unreact = ToolOutput.Text(await _tools.ReactAsync("3EB0", ""));
        var read = ToolOutput.Text(await _tools.MarkReadAsync("Mom"));

        Assert.Multiple(() =>
        {
            Assert.That(react, Is.EqualTo("Reacted with 👍."));
            Assert.That(unreact, Is.EqualTo("Reaction removed."));
            Assert.That(read, Is.EqualTo("Marked as read."));
            Assert.That((string?)_parts.Control.Last("react").Args!["message_id"], Is.EqualTo("3EB0"));
        });
    }

    [Test]
    public async Task Edit_forward_retry_and_download_call_their_operations()
    {
        _parts.Control.Answer("forward_message", """{"forwarded":2}""");

        var edit = ToolOutput.Text(await _tools.EditMessageAsync(null, "3EB0", "fixed"));
        var forward = ToolOutput.Text(await _tools.ForwardMessageAsync(null, "3EB0", ["Mom", "Dad"]));
        var retry = ToolOutput.Text(await _tools.RetryMessageAsync(null, "3EB0"));
        var download = ToolOutput.Text(await _tools.DownloadMediaAsync(null, "3EB0"));

        Assert.Multiple(() =>
        {
            Assert.That(edit, Is.EqualTo("Edited."));
            Assert.That(forward, Is.EqualTo("Forwarded to 2 chats."));
            Assert.That(_parts.Control.Last("forward_message").Args!["chats"]!.AsArray().Select(n => (string?)n), Is.EqualTo(new[] { "Mom", "Dad" }));
            Assert.That(retry, Does.Contain("again"));
            Assert.That(download, Does.Contain("background"));
        });
    }
}
