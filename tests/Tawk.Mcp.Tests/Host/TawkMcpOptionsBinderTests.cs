using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Host;

namespace Tawk.Mcp.Tests.Host;

public class TawkMcpOptionsBinderTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

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
}
