using Tawk.Mcp.Core;
using Tawk.Mcp.Managers.Approvals;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers;

public class ApprovalManagerTests
{
    private readonly ManualTimeProvider _clock = new();

    [Test]
    public async Task Approves_with_the_token_tawk_wrote()
    {
        var approvals = new FakeTawkApprovals();
        var manager = new ApprovalManager(approvals, new FakeAdminTokenSource("secret"), _clock);

        var text = await manager.ApproveAsync(" 7 ", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(approvals.Approved, Is.EqualTo(new[] { ("7", "secret") }));
            Assert.That(text, Does.Contain("3EB0D41C22"));
        });
    }

    [Test]
    public async Task Without_a_token_nothing_is_asked_of_tawk()
    {
        var approvals = new FakeTawkApprovals();
        var manager = new ApprovalManager(approvals, new FakeAdminTokenSource(null), _clock);

        var text = await manager.ApproveAsync("7", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(approvals.Approved, Is.Empty);
            Assert.That(text, Does.Contain("still waits for the user"));
        });
    }

    [Test]
    public async Task A_refusal_says_the_request_still_waits_for_the_user()
    {
        var approvals = new FakeTawkApprovals
        {
            Refusal = new TawkControlException(ControlErrorCode.RateLimited, "This hour's self-approvals are used up"),
        };
        var manager = new ApprovalManager(approvals, new FakeAdminTokenSource("secret"), _clock);

        var text = await manager.ApproveAsync("7", CancellationToken.None);

        Assert.That(text, Does.Contain("still waits for the user").And.Contain("used up"));
    }

    [Test]
    public void Lists_what_waits_and_what_was_answered_meanwhile()
    {
        var approvals = new FakeTawkApprovals();
        approvals.Waiting.Add(new WaitingRequest("7", "send_message", _clock.GetUtcNow().AddSeconds(-30), null));
        approvals.Waiting.Add(new WaitingRequest("9", "react", _clock.GetUtcNow().AddSeconds(-5), "declined"));
        var manager = new ApprovalManager(approvals, new FakeAdminTokenSource("secret"), _clock);

        var text = manager.ListWaiting();

        Assert.That(text, Is.EqualTo("- request 7: send_message, waiting for 30 s\n- request 9: react, answered in tawk meanwhile: declined"));
    }

    [Test]
    public void The_token_is_read_from_its_file_each_time_and_only_when_admin_is_set()
    {
        var path = Path.Combine(Path.GetTempPath(), "tawk-admin-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = new FileAdminTokenSource(new AdminOptions(path));
            var before = source.Read();
            File.WriteAllText(path, "abc123\n");
            var first = source.Read();
            File.WriteAllText(path, "def456\n");

            Assert.Multiple(() =>
            {
                Assert.That(before, Is.Null);
                Assert.That(first, Is.EqualTo("abc123"));
                Assert.That(source.Read(), Is.EqualTo("def456"));
                Assert.That(new FileAdminTokenSource(new AdminOptions(null)).Read(), Is.Null);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }
}
