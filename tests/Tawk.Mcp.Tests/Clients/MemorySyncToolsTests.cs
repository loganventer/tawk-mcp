using Tawk.Mcp.Clients.Tools;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Managers.Sync;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class MemorySyncToolsTests
{
    private sealed class FixedSync(SyncReport report) : IMemorySyncManager
    {
        public int Calls { get; private set; }

        public Task<SyncReport> SyncAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(report);
        }
    }

    [Test]
    public async Task One_sync_is_run_and_what_it_did_is_said()
    {
        var sync = new FixedSync(new SyncReport(SyncOutcome.PulledAndPushed, "Merged 3 changes from the repository and pushed 2."));

        var result = await new MemorySyncTools(sync).SyncMemoryAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sync.Calls, Is.EqualTo(1));
            Assert.That(result.IsError, Is.False);
            Assert.That(ToolOutput.Text(result), Is.EqualTo("Merged 3 changes from the repository and pushed 2."));
        });
    }

    [Test]
    public async Task With_no_repository_it_says_so_and_is_not_an_error()
    {
        var sync = new FixedSync(new SyncReport(SyncOutcome.Disabled, "Memory sync is off: it needs a repository."));

        var result = await new MemorySyncTools(sync).SyncMemoryAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That(ToolOutput.Text(result), Does.StartWith("Memory sync is off"));
        });
    }

    [Test]
    public async Task A_sync_that_failed_is_an_error_with_its_reason()
    {
        var sync = new FixedSync(new SyncReport(SyncOutcome.Failed, "The repository could not be reached."));

        var result = await new MemorySyncTools(sync).SyncMemoryAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(ToolOutput.Text(result), Is.EqualTo("The repository could not be reached."));
        });
    }
}
