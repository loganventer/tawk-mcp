using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Core.Transcription;

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

    /// <summary>Also push read receipts for the user's messages as channel events. Off by default.</summary>
    public bool ChannelRead { get; init; }

    /// <summary>Also push reactions to the user's messages as channel events. Off by default.</summary>
    public bool ChannelReactions { get; init; }

    /// <summary>Also push other people's edits and deletes as channel events. Off by default.</summary>
    public bool ChannelEdits { get; init; }

    /// <summary>Also push a channel event when a scheduled message goes out. Off by default.</summary>
    public bool ChannelScheduled { get; init; }

    /// <summary>Scheduled messages are moved by a random amount up to this many seconds either way. 0 turns it off.</summary>
    public int ScheduleJitterS { get; init; } = 60;

    /// <summary>The folder an Open Knowledge Format bundle is exported to or imported from.</summary>
    public string? BundlePath { get; init; }

    /// <summary>Whether an export also holds what is marked sensitive.</summary>
    public bool IncludeSensitive { get; init; }

    /// <summary>
    /// The private git repository that holds the memory database, as an SSH address. Memory does not sync
    /// without it. There is no default, and access is by SSH key, never a token.
    /// </summary>
    public string? SyncRepository { get; init; }

    /// <summary>A private key file for sync, when SSH should not pick one by itself.</summary>
    public string? SyncKeyFile { get; init; }

    public string SyncBranch { get; init; } = SyncOptions.DefaultBranch;

    public string SyncFile { get; init; } = SyncOptions.DefaultFile;

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

    /// <summary>How voice notes are transcribed. Off by default, and then there are no transcription tools.</summary>
    public TranscriptionEngine Transcribe { get; init; } = TranscriptionEngine.Off;

    /// <summary>The transcriber's base address for the http engine. On this machine unless <see cref="TranscribeRemote"/> is on.</summary>
    public Uri? TranscribeUrl { get; init; }

    /// <summary>Lets the http engine send voice notes to another machine. Off by default.</summary>
    public bool TranscribeRemote { get; init; }

    /// <summary>The program and arguments for the command engine, with {file}, {language}, {model}, {task} and {prompt}.</summary>
    public string? TranscribeCommand { get; init; }

    /// <summary>The model used when a call names none and tawk has no setting for it. tawk's settings panel wins when it has one.</summary>
    public string? TranscribeModel { get; init; }

    /// <summary>The model names an agent may ask for, besides the default.</summary>
    public IReadOnlyList<string> TranscribeModels { get; init; } = [];

    /// <summary>The languages used when a call names none and tawk has no setting for them. tawk's settings panel wins when it has one.</summary>
    public IReadOnlyList<string>? TranscribeLanguages { get; init; }

    public int TranscribeMaxLanguages { get; init; } = TranscriptionOptions.DefaultMaxLanguages;

    public int TranscribeMaxSeconds { get; init; } = TranscriptionOptions.DefaultMaxSeconds;

    public int TranscribeTimeoutS { get; init; } = TranscriptionOptions.DefaultTimeoutS;

    /// <summary>Transcribe every voice note another person sends, without being asked. Only for a tawk without the switch: tawk's settings panel wins when it has one.</summary>
    public bool? TranscribeAuto { get; init; }

    public int TranscribeConcurrency { get; init; } = 1;

    public TimeSpan Heartbeat { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Set when the arguments could not be understood.</summary>
    public string? Error { get; init; }
}
