using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;

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

    /// <summary>Also push the messages the user sends themselves as channel events. Off by default.</summary>
    public bool ChannelOwn { get; init; }

    /// <summary>Scheduled messages are moved by a random amount up to this many seconds either way. 0 turns it off.</summary>
    public int ScheduleJitterS { get; init; } = 60;

    /// <summary>The folder an Open Knowledge Format bundle is exported to or imported from.</summary>
    public string? BundlePath { get; init; }

    /// <summary>Whether an export also holds what is marked sensitive.</summary>
    public bool IncludeSensitive { get; init; }

    /// <summary>A token with contents read and write on the memory repository. Memory does not sync without it.</summary>
    public string? SyncToken { get; init; }

    /// <summary>The private GitHub repository that holds the memory database, as owner/name. Memory does not sync without it.</summary>
    public string? SyncRepository { get; init; }

    public string SyncBranch { get; init; } = SyncOptions.DefaultBranch;

    public string SyncFile { get; init; } = SyncOptions.DefaultFile;

    public string SyncApi { get; init; } = SyncOptions.DefaultApi;

    public int SyncIntervalMinutes { get; init; } = SyncOptions.DefaultIntervalMinutes;

    /// <summary>The agent is handed the memory workflow once every this many rounds. 0 turns it off.</summary>
    public int WorkflowEvery { get; init; } = WorkflowOptions.DefaultEvery;

    /// <summary>A text file of the user's own standing instructions for agents. Defaults to ~/.config/tawk-mcp/instructions.md.</summary>
    public string? InstructionsFile { get; init; }

    /// <summary>
    /// tawk's admin token file. Set per instance and never by default: with it, this instance may answer its
    /// own waiting sends while tawk's access is admin; without it, every write waits for the user.
    /// </summary>
    public string? AdminTokenFile { get; init; }

    /// <summary>What that file held when the server started.</summary>
    public string? UserInstructions { get; init; }

    public TimeSpan Heartbeat { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Set when the arguments could not be understood.</summary>
    public string? Error { get; init; }
}
