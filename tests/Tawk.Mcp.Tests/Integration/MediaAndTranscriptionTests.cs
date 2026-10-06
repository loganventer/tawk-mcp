using System.Text.Json;
using ModelContextProtocol.Protocol;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Integration;

/// <summary>Pictures and voice notes end to end: an MCP client, the real server, a fake tawk and real files.</summary>
[CancelAfter(30000)]
public class MediaAndTranscriptionTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp() => _folder = Directory.CreateTempSubdirectory("tawk-mcp-media").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task View_image_hands_the_picture_to_the_client()
    {
        var picture = Path.Combine(_folder, "a.jpg");
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];
        await File.WriteAllBytesAsync(picture, jpeg);
        await using var harness = new McpHarness();
        harness.Server.Answer("download_media", JsonSerializer.Serialize(new { path = picture, type = "image" }));
        await harness.StartAsync();

        var result = await harness.Client.CallToolAsync("view_image", new Dictionary<string, object?> { ["messageId"] = "3EB0" });

        var image = result.Content.OfType<ImageContentBlock>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True);
            Assert.That(image.MimeType, Is.EqualTo("image/jpeg"));
            Assert.That(image.DecodedData.ToArray(), Is.EqualTo(jpeg));
            Assert.That(result.Content.OfType<TextContentBlock>().Select(b => b.Text), Has.None.Contains(_folder), "the path stays inside tawk-mcp");
        });
    }

    [Test]
    public async Task Transcribe_message_answers_at_once_and_the_text_follows_as_one_channel_event()
    {
        var voice = Path.Combine(_folder, "voice.ogg");
        await File.WriteAllBytesAsync(voice, "OggS"u8.ToArray());
        var script = Path.Combine(_folder, "say.sh");
        await File.WriteAllTextAsync(script, "#!/bin/sh\necho \"said in $1\"\n");
        await using var harness = new McpHarness();
        harness.Server.Answer("download_media", JsonSerializer.Serialize(
            new { path = voice, type = "audio", chat = new { jid = Samples.MomJid, name = "Mom" } }));
        await harness.StartAsync(configure: o => o with
        {
            Transcribe = TranscriptionEngine.Command,
            TranscribeCommand = $"sh {script} {{language}}",
        });

        var names = (await harness.Client.ListToolsAsync()).Select(t => t.Name).ToList();
        var started = await harness.Client.CallToolAsync(
            "transcribe_message", new Dictionary<string, object?> { ["messageId"] = "3EB0", ["languages"] = new[] { "af", "en" } });
        var notification = await harness.WaitForNotificationAsync("notifications/claude/channel", 15000);
        var read = await harness.Client.CallToolAsync("get_transcript", new Dictionary<string, object?> { ["jobId"] = "t1" });

        var content = (string?)notification.Params!["content"];
        var meta = notification.Params["meta"]!;
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("transcribe_message").And.Contain("get_transcript"));
            Assert.That(harness.Client.ServerInstructions, Does.Contain("transcribe_message"));
            Assert.That(ToolOutput.Text(started), Does.Contain("job t1 (af, en)"));
            Assert.That((string?)meta["type"], Is.EqualTo("transcript"));
            Assert.That((string?)meta["status"], Is.EqualTo("done"));
            Assert.That((string?)meta["languages"], Is.EqualTo("af,en"));
            Assert.That((string?)meta["model"], Is.EqualTo("tiny"), "the smallest model unless the user chose another");
            Assert.That((string?)meta["chat_name"], Is.EqualTo("Mom"));
            Assert.That(content, Does.Contain("said in af").And.Contain("said in en").And.Contain("<<<BEGIN UNTRUSTED"));
            Assert.That(content, Does.StartWith(ToolOutput.Text(read)), "get_transcript reads the same text the event carried");
        });
    }
}
