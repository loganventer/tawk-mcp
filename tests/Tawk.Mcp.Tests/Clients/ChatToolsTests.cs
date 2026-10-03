using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Tests.Fakes;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Clients;

public class ChatToolsTests
{
    private TestParts _parts = null!;
    private ChatTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _parts = new TestParts();
        _tools = new ChatTools(_parts.Reading, _parts.Chats, new AmbientAccountScope());
    }

    [Test]
    public async Task List_chats_passes_its_arguments_and_fences_the_answer()
    {
        _parts.Control.Answer("list_chats", $$"""{"chats":[{{Samples.Chat}}]}""");

        var result = await _tools.ListChatsAsync("mo", true, 900);
        var args = _parts.Control.Last("list_chats").Args!;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That((string?)args["filter"], Is.EqualTo("mo"));
            Assert.That((bool?)args["unread_only"], Is.True);
            Assert.That((int?)args["limit"], Is.EqualTo(500));
            Assert.That(ToolOutput.Text(result), Does.Contain("Mom <27820000000@s.whatsapp.net> (2 unread, pinned)").And.Contain("UNTRUSTED"));
        });
    }

    [Test]
    public async Task Read_messages_returns_a_transcript_and_the_next_page()
    {
        _parts.Control.Answer("read_messages", $$"""{"chat":{{Samples.Chat}},"messages":[{{Samples.Message}}],"next_before":1789990000}""");

        var result = await _tools.ReadMessagesAsync("Mom", 1790000001, 20);
        var text = ToolOutput.Text(result);

        Assert.Multiple(() =>
        {
            Assert.That((long?)_parts.Control.Last("read_messages").Args!["before"], Is.EqualTo(1790000001));
            Assert.That(text, Does.Contain("Mom (replying to You: \"When?\"): See you at 6"));
            Assert.That(text, Does.Contain("before=1789990000"));
        });
    }

    [Test]
    public async Task Search_unread_and_chat_info_call_the_right_operations()
    {
        _parts.Control
            .Answer("search_messages", $$"""{"messages":[{{Samples.Message}}]}""")
            .Answer("unread_summary", """{"total":2,"mentions":1,"chats":[]}""")
            .Answer("chat_info", $$"""{"chat":{{Samples.Chat}},"about":"Hi","members":[{"jid":"x@s.whatsapp.net","name":"Dad","admin":true}]}""");

        var search = ToolOutput.Text(await _tools.SearchMessagesAsync("six", "Mom", 5));
        var unread = ToolOutput.Text(await _tools.UnreadSummaryAsync());
        var info = ToolOutput.Text(await _tools.GetChatInfoAsync("Mom"));

        Assert.Multiple(() =>
        {
            Assert.That((string?)_parts.Control.Last("search_messages").Args!["query"], Is.EqualTo("six"));
            Assert.That(search, Does.Contain("Mom in 27820000000@s.whatsapp.net"));
            Assert.That(unread, Does.StartWith("2 unread messages, 1 mentioning you"));
            Assert.That(info, Does.Contain("About: Hi").And.Contain("- Dad <x@s.whatsapp.net> (admin)"));
        });
    }

    [Test]
    public async Task Set_chat_sends_only_the_given_fields()
    {
        _parts.Control.Answer("set_chat", $$"""{"chat":{{Samples.Chat}}}""");

        var result = await _tools.SetChatAsync(null, "Mom", "3600", pinned: true);
        var args = _parts.Control.Last("set_chat").Args!;

        Assert.Multiple(() =>
        {
            Assert.That(ToolOutput.Text(result), Does.StartWith("Updated."));
            Assert.That((long?)args["muted"], Is.EqualTo(3600));
            Assert.That((bool?)args["pinned"], Is.True);
            Assert.That(args.ContainsKey("archived"), Is.False);
        });
    }

    [Test]
    public async Task Export_and_unblock_report_the_outcome()
    {
        _parts.Control.Answer("export_chat", """{"path":"/home/me/Downloads/Mom"}""");

        Assert.Multiple(async () =>
        {
            Assert.That(ToolOutput.Text(await _tools.ExportChatAsync(null, "Mom", true)), Is.EqualTo("Exported to /home/me/Downloads/Mom."));
            Assert.That(ToolOutput.Text(await _tools.UnblockAsync(null, "Mom")), Is.EqualTo("Unblocked."));
            Assert.That(ToolOutput.Text(await _tools.SetChatThemeAsync(null, "Mom", "")), Does.Contain("app theme"));
        });
    }
}
