using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Tawk.Mcp.Clients;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Integration;

[CancelAfter(30000)]
public class McpEndToEndTests
{
    private McpHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = new McpHarness();

    [TearDown]
    public async Task TearDown() => await _harness.DisposeAsync();

    [Test]
    public async Task Sending_says_how_to_name_someone_with_no_chat_yet_and_reading_does_not()
    {
        await _harness.StartAsync();

        var tools = await _harness.Client.ListToolsAsync();
        string Chat(string tool) => tools.Single(t => t.Name == tool).JsonSchema
            .GetProperty("properties").GetProperty("chat").GetProperty("description").GetString()!;

        Assert.Multiple(() =>
        {
            Assert.That(Chat("send_message"), Does.Contain("no chat with yet").And.Contain("country code").And.Contain("contacts"));
            Assert.That(Chat("send_message"), Does.Contain("always the user's own to approve"));
            Assert.That(Chat("schedule_message"), Is.EqualTo(Chat("send_message")));
            Assert.That(Chat("draft_message"), Is.EqualTo("The chat's jid or name."), "a draft needs a chat that is already there");
            Assert.That(Chat("read_messages"), Does.Not.Contain("country code"));
            Assert.That(_harness.Client.ServerInstructions, Does.Contain("no chat with yet"));
        });
    }

    [Test]
    public async Task Offers_every_tool_and_no_way_to_confirm_for_the_user()
    {
        await _harness.StartAsync();

        var tools = await _harness.Client.ListToolsAsync();
        var names = tools.Select(t => t.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(names, Has.Count.EqualTo(90));
            Assert.That(names, Does.Contain("set_summary"));
            Assert.That(names, Does.Contain("list_labels").And.Contain("set_label").And.Contain("list_reminders").And.Contain("set_reminder")
                .And.Contain("cancel_reminder").And.Contain("awaiting_replies"));
            Assert.That(names, Does.Contain("view_image"));
            Assert.That(names, Does.Contain("transcribe_message").And.Contain("get_transcript").And.Contain("get_transcription_progress"), "always offered, so an agent can be told why when it is off");
            Assert.That(names, Does.Contain("draft_message").And.Contain("delete_chat").And.Contain("decline_call"));
            Assert.That(names, Has.None.Contains("confirm"));
            Assert.That(tools.Single(t => t.Name == "read_messages").Description, Does.Contain("untrusted data"));
            Assert.That(tools.Single(t => t.Name == "send_message").Description, Does.Contain("approve it in tawk"));
            Assert.That(_harness.Client.ServerCapabilities.Experimental!.ContainsKey("claude/channel"), Is.True);
            Assert.That(_harness.Client.ServerCapabilities.Resources!.Subscribe, Is.True);
            Assert.That(_harness.Client.ServerInstructions, Does.Contain("untrusted data"));
        });
    }

    [Test]
    public async Task Says_what_tells_it_apart_when_it_greets_tawk()
    {
        await _harness.StartAsync();

        var hello = await _harness.Server.WaitForAsync("hello");

        Assert.That((string?)hello["args"]!["label"], Is.EqualTo("http, port 8765"));
    }

    [Test]
    public async Task A_session_tells_tawk_what_it_is_doing()
    {
        await _harness.StartAsync();

        var result = await _harness.Client.CallToolAsync(
            "describe_session", new Dictionary<string, object?> { ["description"] = "Reviewing the billing service\nin the payments repo" });
        var sent = await _harness.Server.WaitForAsync("describe");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True);
            Assert.That((string?)sent["args"]!["text"], Is.EqualTo("Reviewing the billing service in the payments repo"));
            Assert.That(_harness.Client.ServerInstructions, Does.Contain("describe_session"));
        });
    }

    [Test]
    public async Task A_description_of_more_than_ten_words_is_sent_back_to_be_shortened()
    {
        await _harness.StartAsync();

        var result = await _harness.Client.CallToolAsync(
            "describe_session", new Dictionary<string, object?> { ["description"] = "one two three four five six seven eight nine ten eleven" });
        await _harness.Client.CallToolAsync("app_status");
        await _harness.Server.WaitForAsync("app_status");

        Assert.Multiple(() =>
        {
            Assert.That(string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text)), Does.Contain("11 words").And.Contain("at most 10"));
            Assert.That(_harness.Server.Received.Any(r => (string?)r["op"] == "describe"), Is.False);
        });
    }

    [Test]
    public async Task Get_version_names_this_tawk_mcp_and_the_tawk_it_talks_to()
    {
        await _harness.StartAsync();

        var result = await _harness.Client.CallToolAsync("get_version");
        var text = string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("tawk-mcp " + Tawk.Mcp.Host.TawkMcpComposition.Version));
            Assert.That(text, Does.Contain("tawk 0.6.4 (control protocol 1)"));
        });
    }

    [Test]
    public async Task With_transcription_off_the_tool_is_still_there_and_says_why()
    {
        await using var off = new McpHarness();
        await off.StartAsync(configure: o => o with { Transcribe = Tawk.Mcp.Core.Transcription.TranscriptionEngine.Off });

        var result = await off.Client.CallToolAsync("transcribe_message", new Dictionary<string, object?> { ["messageId"] = "3EB0" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text)), Does.Contain("--transcribe off"));
        });
    }

    [Test]
    public async Task Memory_tools_are_offered_and_vanish_when_memory_is_off()
    {
        await _harness.StartAsync();
        await using var off = new McpHarness();
        await off.StartAsync(memory: Tawk.Mcp.Core.Memory.MemoryMode.Off);

        var names = (await _harness.Client.ListToolsAsync()).Select(t => t.Name).ToList();
        var offNames = (await off.Client.ListToolsAsync()).Select(t => t.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("check_voice").And.Contain("get_contact").And.Contain("draft_template").And.Contain("set_category"));
            Assert.That(names, Does.Contain("record_observation").And.Contain("list_observations").And.Contain("record_relation").And.Contain("get_knowledge"));
            Assert.That(_harness.Client.ServerInstructions, Does.Contain("record_observation"));
            Assert.That(names, Has.None.EqualTo("get_workflow"));
            Assert.That(_harness.Client.ServerInstructions, Does.Not.Contain("workflow check"));
            Assert.That(offNames, Has.Count.EqualTo(57));
            Assert.That(offNames, Has.None.Contains("voice"));
            Assert.That(off.Client.ServerInstructions, Does.Not.Contain("remembers"));
            Assert.That(File.Exists(off.DataFile), Is.False);
        });
    }

    [Test]
    public async Task A_voice_saved_over_mcp_checks_a_draft()
    {
        await _harness.StartAsync();

        await _harness.Client.CallToolAsync("set_voice", new Dictionary<string, object?> { ["name"] = "logan", ["guide"] = "lowercase", ["rules"] = "{\"case\":\"lower\"}" });
        var result = await _harness.Client.CallToolAsync("check_voice", new Dictionary<string, object?> { ["draft"] = "Dear Sir" });
        var text = string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True);
            Assert.That(text, Does.Contain("[warning] case"));
            Assert.That(File.Exists(_harness.DataFile), Is.True);
        });
    }

    [Test]
    public async Task Settles_on_revision_2025_11_25_and_keeps_older_revisions_a_client_asks_for()
    {
        await _harness.StartAsync();
        await using var older = new McpHarness();
        await older.StartAsync(protocolVersion: "2025-06-18");

        Assert.Multiple(() =>
        {
            Assert.That(_harness.Client.NegotiatedProtocolVersion, Is.EqualTo("2025-11-25"));
            Assert.That(older.Client.NegotiatedProtocolVersion, Is.EqualTo("2025-06-18"));
        });
    }

    [Test]
    public async Task A_send_reports_the_approval_wait_as_progress()
    {
        _harness.Server.Hold("send_message");
        await _harness.StartAsync();
        var progress = new List<ProgressNotificationValue>();

        var call = _harness.Client.CallToolAsync(
            "send_message",
            new Dictionary<string, object?> { ["chat"] = "Mom", ["text"] = "On my way" },
            new SyncProgress(progress)).AsTask();
        var id = (string)(await _harness.Server.WaitForAsync("send_message"))["id"]!;
        await _harness.Server.SendAsync($$$"""{"evt":"approval","id":"{{{id}}}","state":"waiting"}""");
        while (progress.Count == 0)
        {
            await Task.Delay(10);
        }

        await _harness.Server.SendAsync($$$"""{"id":"{{{id}}}","ok":true,"result":{"id":"3EB0D41C22"}}""");
        var result = await call;

        Assert.Multiple(() =>
        {
            Assert.That(progress[0].Message, Is.EqualTo(ApprovalProgress.WaitingMessage));
            Assert.That(ToolOutput.Text(result), Does.Contain("3EB0D41C22"));
        });
    }

    [Test]
    public async Task A_destructive_tool_asks_the_user_and_keeps_the_token_away_from_the_model()
    {
        _harness.Server.Answer("delete_chat", """{"needs_confirmation":true,"token":"c1f0deadbeef","summary":"Delete the chat with Mom here and on your phone","expires_at":1790000300}""");
        await _harness.StartAsync(accept: true);

        var result = await _harness.Client.CallToolAsync("delete_chat", new Dictionary<string, object?> { ["chat"] = "Mom" });
        var confirm = await _harness.Server.WaitForAsync("confirm");

        Assert.Multiple(() =>
        {
            Assert.That(_harness.Elicitations.Single().Message, Does.Contain("Delete the chat with Mom"));
            Assert.That((string?)confirm["args"]!["token"], Is.EqualTo("c1f0deadbeef"));
            Assert.That(ToolOutput.Text(result), Does.Not.Contain("c1f0"));
            Assert.That(_harness.Elicitations.Single().Message, Does.Not.Contain("c1f0"));
        });
    }

    [Test]
    public async Task A_client_without_elicitation_is_refused()
    {
        _harness.Server.Answer("block", """{"needs_confirmation":true,"token":"c1f0deadbeef","summary":"Block Mom"}""");
        await _harness.StartAsync(elicitation: false);

        var result = await _harness.Client.CallToolAsync("block", new Dictionary<string, object?> { ["chat"] = "Mom" });
        await _harness.Server.WaitForAsync("cancel_confirmation");

        Assert.Multiple(() =>
        {
            Assert.That(ToolOutput.Text(result), Does.Contain("your MCP client cannot ask you"));
            Assert.That(_harness.Server.Received.Any(r => (string?)r["op"] == "confirm"), Is.False);
        });
    }

    [Test]
    public async Task New_messages_arrive_as_channel_events_and_resource_updates()
    {
        await _harness.StartAsync();
        await _harness.Server.WaitForAsync("subscribe");
        await _harness.Client.SubscribeToResourceAsync("tawk://chat/27820000000@s.whatsapp.net");

        await _harness.Server.SendAsync(Samples.MessageEvent("Are you coming?"));
        var channel = await _harness.WaitForNotificationAsync("notifications/claude/channel");
        var updated = await _harness.WaitForNotificationAsync(NotificationMethods.ResourceUpdatedNotification);

        Assert.Multiple(() =>
        {
            Assert.That((string?)channel.Params!["content"], Does.Contain("Are you coming?").And.Contain("UNTRUSTED CHAT DATA"));
            Assert.That((string?)channel.Params!["meta"]!["chat_name"], Is.EqualTo("Mom"));
            Assert.That((string?)updated.Params!["uri"], Is.EqualTo("tawk://chat/27820000000@s.whatsapp.net"));
        });
    }

    [Test]
    public async Task Resources_and_prompts_read_through_tawk()
    {
        _harness.Server
            .Answer("list_chats", $$"""{"chats":[{{Samples.Chat}}]}""")
            .Answer("read_messages", $$"""{"chat":{{Samples.Chat}},"messages":[{{Samples.Message}}],"next_before":0}""")
            .Answer("unread_summary", Samples.UnreadAnswer[(Samples.UnreadAnswer.IndexOf("\"result\":", StringComparison.Ordinal) + 9)..^1]);
        await _harness.StartAsync();

        var chats = await _harness.Client.ReadResourceAsync("tawk://chats");
        var chat = await _harness.Client.ReadResourceAsync("tawk://chat/27820000000@s.whatsapp.net");
        var catchUp = await _harness.Client.GetPromptAsync("catch_up", new Dictionary<string, object?>());
        var draft = await _harness.Client.GetPromptAsync("draft_reply", new Dictionary<string, object?> { ["chat"] = "Mom" });

        string Text(ReadResourceResult r) => ((TextResourceContents)r.Contents[0]).Text;
        string Prompt(GetPromptResult r) => ((TextContentBlock)r.Messages[0].Content).Text;
        Assert.Multiple(() =>
        {
            Assert.That(Text(chats), Does.Contain("Mom <27820000000@s.whatsapp.net>"));
            Assert.That(Text(chat), Does.Contain("See you at 6"));
            Assert.That(Prompt(catchUp), Does.Contain("read_messages"));
            Assert.That(Prompt(draft), Does.Contain("draft_message").And.Contain("Never call send_message"));
        });
    }
}
