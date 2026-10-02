using ModelContextProtocol.Protocol;
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Host;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Integration;

public class WorkflowTests
{
    private const string Opening = "Workflow check from tawk-mcp.";
    private McpHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = new McpHarness();

    [TearDown]
    public async Task TearDown() => await _harness.DisposeAsync();

    [Test]
    public async Task The_workflow_rides_on_a_tool_result_once_every_so_many_rounds()
    {
        await _harness.StartAsync(workflowEvery: 3);

        var texts = new List<string>();
        for (var i = 0; i < 7; i++)
        {
            texts.Add(await CallAsync("list_categories"));
        }

        Assert.Multiple(() =>
        {
            Assert.That(texts.Select(t => t.Contains(Opening, StringComparison.Ordinal)), Is.EqualTo(new[] { false, false, true, false, false, true, false }));
            Assert.That(texts[2], Does.StartWith(texts[0]));
            Assert.That(_harness.Client.ServerInstructions, Does.Contain("get_workflow"));
        });
    }

    [Test]
    public async Task Asking_for_the_workflow_starts_the_count_again()
    {
        await _harness.StartAsync(workflowEvery: 2, userInstructions: "Send only when I ask.");

        await CallAsync("list_categories");
        var asked = await CallAsync("get_workflow");
        var next = await CallAsync("list_categories");
        var due = await CallAsync("list_categories");

        Assert.Multiple(() =>
        {
            Assert.That(asked, Does.StartWith(Opening).And.Contain("The user's standing instructions").And.EndWith("Send only when I ask."));
            Assert.That(next, Does.Not.Contain(Opening));
            Assert.That(due, Does.Contain(Opening).And.Contain("Send only when I ask."));
        });
    }

    [Test]
    public async Task The_users_own_instructions_become_part_of_the_servers()
    {
        await _harness.StartAsync(userInstructions: "  Never use draft_message.  ");

        Assert.That(_harness.Client.ServerInstructions,
            Does.EndWith("The user's standing instructions for working with tawk, which come before the defaults above:\nNever use draft_message."));
    }

    [Test]
    public async Task There_is_no_workflow_when_memory_cannot_be_written()
    {
        await _harness.StartAsync(memory: MemoryMode.Read, workflowEvery: 1);

        var text = await CallAsync("list_categories");
        var names = (await _harness.Client.ListToolsAsync()).Select(t => t.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Not.Contain(Opening));
            Assert.That(names, Has.None.EqualTo("get_workflow"));
        });
    }

    [Test]
    public void A_failed_call_counts_as_a_round_but_never_carries_the_workflow()
    {
        var cadence = new WorkflowCadence(new WorkflowOptions(2, null));
        cadence.Round();

        Assert.Multiple(() =>
        {
            Assert.That(cadence.TakeDue(), Is.False);
            cadence.Round();
            Assert.That(cadence.TakeDue(), Is.True);
            Assert.That(cadence.TakeDue(), Is.False);
            Assert.That(new WorkflowCadence(new WorkflowOptions(0, null)).TakeDue(), Is.False);
        });
    }

    [Test]
    public void The_instructions_file_is_read_trimmed_and_capped()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tawk-instructions-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "instructions.md");
            File.WriteAllText(path, "\n  Reply in Afrikaans to family.\n\n");
            var big = Path.Combine(folder, "big.md");
            File.WriteAllText(big, new string('x', UserInstructionsFile.MaxLength + 500));

            Assert.Multiple(() =>
            {
                Assert.That(UserInstructionsFile.Read(path), Is.EqualTo("Reply in Afrikaans to family."));
                Assert.That(UserInstructionsFile.Read(big), Has.Length.EqualTo(UserInstructionsFile.MaxLength));
                Assert.That(UserInstructionsFile.Read(Path.Combine(folder, "missing.md")), Is.Null);
                Assert.That(UserInstructionsFile.Read(null), Is.Null);
                Assert.That(UserInstructionsFile.DefaultPath(_ => null, "/home/u"), Is.EqualTo(Path.Combine("/home/u", ".config", "tawk-mcp", "instructions.md")));
                Assert.That(UserInstructionsFile.DefaultPath(k => k == "XDG_CONFIG_HOME" ? "/cfg" : null, "/home/u"), Is.EqualTo(Path.Combine("/cfg", "tawk-mcp", "instructions.md")));
            });
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private async Task<string> CallAsync(string tool)
    {
        var result = await _harness.Client.CallToolAsync(tool, new Dictionary<string, object?>());
        return string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text));
    }
}
