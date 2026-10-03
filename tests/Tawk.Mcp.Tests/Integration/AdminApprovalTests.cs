using ModelContextProtocol.Protocol;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Integration;

[CancelAfter(20000)]
public class AdminApprovalTests
{
    private McpHarness _harness = null!;
    private string _tokenFile = null!;

    [SetUp]
    public void SetUp()
    {
        _harness = new McpHarness();
        _tokenFile = Path.Combine(Path.GetTempPath(), "tawk-admin-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _harness.DisposeAsync();
        File.Delete(_tokenFile);
    }

    [Test]
    public async Task Without_an_admin_token_file_there_is_no_way_to_approve()
    {
        await _harness.StartAsync();

        var names = (await _harness.Client.ListToolsAsync()).Select(t => t.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(names, Has.None.EqualTo("approve_pending").And.None.EqualTo("list_pending"));
            Assert.That(_harness.Client.ServerInstructions, Does.Not.Contain("approve_pending"));
        });
    }

    [Test]
    public async Task A_queued_send_comes_back_waiting_and_is_approved_with_the_token()
    {
        File.WriteAllText(_tokenFile, "s3cret\n");
        _harness.Server.Hold("send_message").Hold("approve");
        await _harness.StartAsync(adminTokenFile: _tokenFile);

        var send = CallAsync("send_message", new() { ["chat"] = "Mom", ["text"] = "On my way" });
        var id = (string)(await _harness.Server.WaitForAsync("send_message"))["id"]!;
        await _harness.Server.SendAsync($$$"""{"evt":"approval","id":"{{{id}}}","state":"waiting"}""");
        var waiting = await send;
        var listed = await CallAsync("list_pending", []);

        var approve = CallAsync("approve_pending", new() { ["id"] = id });
        var asked = await _harness.Server.WaitForAsync("approve");
        await _harness.Server.SendAsync($$$"""{"id":"{{{id}}}","ok":true,"result":{"id":"3EB0D41C22"}}""");
        await _harness.Server.SendAsync($$$"""{"id":"{{{(string)asked["id"]!}}}","ok":true,"result":{"approved":true}}""");
        var approved = await approve;

        Assert.Multiple(() =>
        {
            Assert.That(waiting.IsError, Is.Not.True);
            Assert.That(Text(waiting), Does.Contain($"request {id}").And.Contain("approve_pending"));
            Assert.That(Text(listed), Does.Contain($"request {id}: send_message"));
            Assert.That((string?)asked["args"]!["admin_token"], Is.EqualTo("s3cret"));
            Assert.That(Text(approved), Does.Contain("3EB0D41C22"));
            Assert.That(_harness.Client.ServerInstructions, Does.Contain("approve_pending"));
        });
    }

    private static string Text(CallToolResult result) =>
        string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    private async Task<CallToolResult> CallAsync(string tool, Dictionary<string, object?> args) =>
        await _harness.Client.CallToolAsync(tool, args);
}
