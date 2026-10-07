using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Host;

namespace Tawk.Mcp.Tests.Host;

public class TawkMcpOptionsBinderTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

    [TestCase("--port", "--stdio", "--version")]
    [TestCase("-port", "-stdio", "-version")]
    [TestCase("port", "stdio", "version")]
    public void An_option_may_be_written_with_two_dashes_one_or_none(string port, string stdio, string version)
    {
        var options = TawkMcpOptionsBinder.Bind([port, "9000", stdio, version], Env());

        Assert.Multiple(() =>
        {
            Assert.That(options.Error, Is.Null);
            Assert.That(options.Port, Is.EqualTo(9000));
            Assert.That(options.Transport, Is.EqualTo(TransportKind.Stdio));
            Assert.That(options.Command, Is.EqualTo(HostCommand.Version));
        });
    }

    [TestCase("print-token")]
    [TestCase("-print-token")]
    [TestCase("--print-token")]
    public void A_command_may_be_written_the_same_three_ways(string command)
    {
        Assert.That(TawkMcpOptionsBinder.Bind([command], Env()).Command, Is.EqualTo(HostCommand.PrintToken));
    }

    [Test]
    public void A_value_keeps_its_dashes_and_an_unknown_word_is_still_refused()
    {
        var options = TawkMcpOptionsBinder.Bind(["--token-file", "--odd-name", "-v"], Env());

        Assert.Multiple(() =>
        {
            Assert.That(options.TokenFile, Is.EqualTo("--odd-name"));
            Assert.That(options.Command, Is.EqualTo(HostCommand.Version));
            Assert.That(TawkMcpOptionsBinder.Bind(["---port", "9000"], Env()).Error, Does.Contain("Unknown argument"));
            Assert.That(TawkMcpOptionsBinder.Bind(["nonsense"], Env()).Error, Does.Contain("Unknown argument: nonsense"));
        });
    }

    [Test]
    public void Defaults_to_http_on_loopback_with_the_documented_timings()
    {
        var options = TawkMcpOptionsBinder.Bind([], Env());

        Assert.Multiple(() =>
        {
            Assert.That(options.Error, Is.Null);
            Assert.That(options.Transport, Is.EqualTo(TransportKind.Http));
            Assert.That(options.Bind, Is.EqualTo("127.0.0.1"));
            Assert.That(options.Port, Is.EqualTo(8765));
            Assert.That(options.BackoffInitialMs, Is.EqualTo(500));
            Assert.That(options.BackoffMaxMs, Is.EqualTo(30000));
            Assert.That(options.BreakerThreshold, Is.EqualTo(5));
            Assert.That(options.BreakerCooldownS, Is.EqualTo(60));
            Assert.That(options.RequestTimeoutS, Is.EqualTo(10));
            Assert.That(options.Channel, Is.EqualTo(ChannelMode.Auto));
        });
    }

    [Test]
    public void Reads_the_environment()
    {
        var options = TawkMcpOptionsBinder.Bind([], Env(
            ("TAWKMCP_TRANSPORT", "Http"), ("TAWKMCP_BIND", "0.0.0.0"), ("TAWKMCP_PORT", "9000"),
            ("TAWKMCP_TOKEN_FILE", "/data/token"), ("TAWKMCP_BACKOFF_INITIAL_MS", "250"), ("TAWKMCP_BACKOFF_MAX_MS", "5000"),
            ("TAWKMCP_BREAKER_THRESHOLD", "3"), ("TAWKMCP_BREAKER_COOLDOWN_S", "20"), ("TAWKMCP_REQUEST_TIMEOUT_S", "4"),
            ("TAWKMCP_CHANNEL", "off")));

        Assert.Multiple(() =>
        {
            Assert.That(options.Transport, Is.EqualTo(TransportKind.Http));
            Assert.That(options.Bind, Is.EqualTo("0.0.0.0"));
            Assert.That(options.Port, Is.EqualTo(9000));
            Assert.That(options.TokenFile, Is.EqualTo("/data/token"));
            Assert.That(options.BackoffInitialMs, Is.EqualTo(250));
            Assert.That(options.BackoffMaxMs, Is.EqualTo(5000));
            Assert.That(options.BreakerThreshold, Is.EqualTo(3));
            Assert.That(options.BreakerCooldownS, Is.EqualTo(20));
            Assert.That(options.RequestTimeoutS, Is.EqualTo(4));
            Assert.That(options.Channel, Is.EqualTo(ChannelMode.Off));
        });
    }

    [Test]
    public void Flags_override_the_environment()
    {
        var options = TawkMcpOptionsBinder.Bind(
            ["--http", "--port", "8800", "--socket", "/tmp/c.sock", "--backoff-initial-ms", "100", "--channel", "on"],
            Env(("TAWKMCP_PORT", "9000"), ("TAWKMCP_CHANNEL", "off")));

        Assert.Multiple(() =>
        {
            Assert.That(options.Transport, Is.EqualTo(TransportKind.Http));
            Assert.That(options.Port, Is.EqualTo(8800));
            Assert.That(options.SocketPath, Is.EqualTo("/tmp/c.sock"));
            Assert.That(options.BackoffInitialMs, Is.EqualTo(100));
            Assert.That(options.Channel, Is.EqualTo(ChannelMode.On));
        });
    }

    [Test]
    public void Stdio_is_chosen_by_flag_or_environment()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TawkMcpOptionsBinder.Bind(["--stdio"], Env()).Transport, Is.EqualTo(TransportKind.Stdio));
            Assert.That(TawkMcpOptionsBinder.Bind([], Env(("TAWKMCP_TRANSPORT", "stdio"))).Transport, Is.EqualTo(TransportKind.Stdio));
            Assert.That(TawkMcpOptionsBinder.Bind(["--http"], Env(("TAWKMCP_TRANSPORT", "stdio"))).Transport, Is.EqualTo(TransportKind.Http));
        });
    }

    [TestCase("print-token", HostCommand.PrintToken)]
    [TestCase("healthcheck", HostCommand.Healthcheck)]
    [TestCase("--version", HostCommand.Version)]
    [TestCase("--help", HostCommand.Help)]
    public void Recognises_commands(string arg, HostCommand command)
    {
        Assert.That(TawkMcpOptionsBinder.Bind([arg], Env()).Command, Is.EqualTo(command));
    }

    [Test]
    public void Reports_bad_input()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TawkMcpOptionsBinder.Bind(["--port", "nope"], Env()).Error, Does.Contain("--port"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--port"], Env()).Error, Does.Contain("needs a value"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--frobnicate"], Env()).Error, Does.Contain("Unknown argument"));
            Assert.That(TawkMcpOptionsBinder.Bind([], Env(("TAWKMCP_CHANNEL", "maybe"))).Error, Does.Contain("auto, on or off"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--backoff-initial-ms", "900", "--backoff-max-ms", "100"], Env()).Error, Does.Contain("maximum"));
        });
    }

    [Test]
    public void Memory_and_jitter_options_bind_from_flags_and_the_environment()
    {
        var env = new Dictionary<string, string?> { ["TAWKMCP_MEMORY"] = "read", ["TAWKMCP_SCHEDULE_JITTER_S"] = "30" };
        var fromEnv = TawkMcpOptionsBinder.Bind([], name => env.GetValueOrDefault(name));
        var fromFlags = TawkMcpOptionsBinder.Bind(["--memory", "off", "--data-file", "/tmp/m.db", "--schedule-jitter-s", "0"], _ => null);

        Assert.Multiple(() =>
        {
            Assert.That(fromEnv.Memory, Is.EqualTo(Tawk.Mcp.Core.Memory.MemoryMode.Read));
            Assert.That(fromEnv.ScheduleJitterS, Is.EqualTo(30));
            Assert.That(fromFlags.Memory, Is.EqualTo(Tawk.Mcp.Core.Memory.MemoryMode.Off));
            Assert.That(fromFlags.DataFile, Is.EqualTo("/tmp/m.db"));
            Assert.That(fromFlags.ScheduleJitterS, Is.Zero);
            Assert.That(new TawkMcpOptions().Memory, Is.EqualTo(Tawk.Mcp.Core.Memory.MemoryMode.Write));
            Assert.That(TawkMcpOptionsBinder.Bind(["--memory", "maybe"], _ => null).Error, Does.Contain("write, read or off"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--schedule-jitter-s", "9000"], _ => null).Error, Is.Not.Null);
        });
    }

    [Test]
    public void Sync_workflow_and_bundle_options_bind()
    {
        var defaults = TawkMcpOptionsBinder.Bind([], Env());
        var fromEnv = TawkMcpOptionsBinder.Bind([], Env(
            ("TAWKMCP_SYNC_KEY", "/k/id"), ("TAWKMCP_SYNC_REPO", "me/mem"), ("TAWKMCP_SYNC_BRANCH", "trunk"),
            ("TAWKMCP_SYNC_INTERVAL_MINUTES", "30"), ("TAWKMCP_WORKFLOW_EVERY", "0"), ("TAWKMCP_INSTRUCTIONS_FILE", "/x/i.md")));
        var flags = TawkMcpOptionsBinder.Bind(
            ["export-okf", "/tmp/out", "--include-sensitive", "--sync-repo", "a/b", "--sync-interval-minutes", "5", "--workflow-every", "7"], Env());

        Assert.Multiple(() =>
        {
            Assert.That((defaults.SyncKeyFile, defaults.SyncRepository, defaults.SyncBranch, defaults.SyncIntervalMinutes, defaults.WorkflowEvery),
                Is.EqualTo(((string?)null, (string?)null, "main", 120, 20)));
            Assert.That((fromEnv.SyncKeyFile, fromEnv.SyncRepository, fromEnv.SyncBranch, fromEnv.SyncIntervalMinutes, fromEnv.WorkflowEvery, fromEnv.InstructionsFile),
                Is.EqualTo(((string?)"/k/id", (string?)"git@github.com:me/mem.git", "trunk", 30, 0, (string?)"/x/i.md")));
            Assert.That((flags.Command, flags.BundlePath, flags.IncludeSensitive, flags.SyncRepository, flags.SyncIntervalMinutes, flags.WorkflowEvery),
                Is.EqualTo((HostCommand.ExportOkf, (string?)"/tmp/out", true, (string?)"git@github.com:a/b.git", 5, 7)));
            Assert.That(TawkMcpOptionsBinder.Bind(["import-okf", "/in"], Env()).Command, Is.EqualTo(HostCommand.ImportOkf));
            Assert.That(TawkMcpOptionsBinder.Bind(["sync"], Env()).Command, Is.EqualTo(HostCommand.Sync));
            Assert.That(TawkMcpOptionsBinder.Bind(["export-okf"], Env()).Error, Does.Contain("needs a value"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--sync-repo", "https://github.com/a/b"], Env()).Error, Does.Contain("SSH address"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--sync-repo", "git@my-alias:a/b.git"], Env()).SyncRepository, Is.EqualTo("git@my-alias:a/b.git"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--sync-repo", "ssh://git@host.example/a/b.git"], Env()).Error, Is.Null);
            Assert.That(TawkMcpOptionsBinder.Bind(["--sync-repo", "--upload-pack=x"], Env()).Error, Is.Not.Null);
            Assert.That(TawkMcpOptionsBinder.Bind(["--sync-token", "t"], Env()).Error, Does.Contain("Unknown argument"));
            Assert.That(defaults.SyncFile, Is.EqualTo("memory.db"));
            var elsewhere = TawkMcpOptionsBinder.Bind(["--sync-file", "data/mem.db", "--sync-key", "/k/other"], Env());
            Assert.That((elsewhere.Error, elsewhere.SyncFile, elsewhere.SyncKeyFile), Is.EqualTo(((string?)null, "data/mem.db", (string?)"/k/other")));
            Assert.That(TawkMcpOptionsBinder.Bind(["--sync-file", "../memory.db"], Env()).Error, Does.Contain("inside the repository"));
            Assert.That(TawkMcpOptionsBinder.Bind(["--sync-api", "https://x.example"], Env()).Error, Does.Contain("Unknown argument"));
        });
    }

    [Test]
    public async Task The_memory_commands_work_without_a_server()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tawk-command-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var options = new TawkMcpOptions { DataFile = Path.Combine(folder, "memory.db"), BundlePath = Path.Combine(folder, "bundle") };
            using var output = new StringWriter();

            var exported = await MemoryCommand.RunAsync(options with { Command = HostCommand.ExportOkf }, output);
            var again = await MemoryCommand.RunAsync(options with { Command = HostCommand.ExportOkf }, output);
            var imported = await MemoryCommand.RunAsync(options with { Command = HostCommand.ImportOkf }, output);
            var synced = await MemoryCommand.RunAsync(options with { Command = HostCommand.Sync }, output);

            Assert.Multiple(() =>
            {
                Assert.That((exported, again, imported, synced), Is.EqualTo((0, 1, 0, 1)));
                Assert.That(File.Exists(Path.Combine(folder, "bundle", "index.md")), Is.True);
                Assert.That(output.ToString(), Does.Contain("Exported 0 concept(s)").And.Contain("is not empty").And.Contain("Read 0 concept(s)").And.Contain("Memory sync is off"));
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public void Online_status_events_are_off_until_asked_for_by_flag_or_environment()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TawkMcpOptionsBinder.Bind([], Env()).ChannelPresence, Is.False);
            Assert.That(TawkMcpOptionsBinder.Bind(["--channel-presence", "on"], Env()).ChannelPresence, Is.True);
            Assert.That(TawkMcpOptionsBinder.Bind([], Env(("TAWKMCP_CHANNEL_PRESENCE", "on"))).ChannelPresence, Is.True);
            Assert.That(TawkMcpOptionsBinder.Bind(["--channel-presence", "off"], Env(("TAWKMCP_CHANNEL_PRESENCE", "on"))).ChannelPresence, Is.False);
        });
    }
}
