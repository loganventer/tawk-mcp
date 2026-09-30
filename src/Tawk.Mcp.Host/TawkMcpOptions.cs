using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Host;

/// <summary>Everything tawk-mcp can be told, from TAWKMCP_* environment variables and command-line flags.</summary>
public sealed record TawkMcpOptions
{
    public HostCommand Command { get; init; } = HostCommand.Serve;

    public TransportKind Transport { get; init; } = TransportKind.Http;

    public int Port { get; init; } = 8765;

    public string Bind { get; init; } = "127.0.0.1";

    public string? SocketPath { get; init; }

    public string? TokenFile { get; init; }

    public string? Token { get; init; }

    /// <summary>Where voices, contact profiles and templates are kept. Defaults to ~/.local/share/tawk-mcp/memory.db.</summary>
    public string? DataFile { get; init; }

    public MemoryMode Memory { get; init; } = MemoryMode.Write;

    public int BackoffInitialMs { get; init; } = 500;

    public int BackoffMaxMs { get; init; } = 30_000;

    public int BreakerThreshold { get; init; } = 5;

    public int BreakerCooldownS { get; init; } = 60;

    public int RequestTimeoutS { get; init; } = 10;

    public ChannelMode Channel { get; init; } = ChannelMode.Auto;

    /// <summary>Scheduled messages are moved by a random amount up to this many seconds either way. 0 turns it off.</summary>
    public int ScheduleJitterS { get; init; } = 60;

    public TimeSpan Heartbeat { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Set when the arguments could not be understood.</summary>
    public string? Error { get; init; }
}
