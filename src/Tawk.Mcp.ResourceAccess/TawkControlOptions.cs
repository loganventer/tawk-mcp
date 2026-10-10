namespace Tawk.Mcp.ResourceAccess;

public sealed record TawkControlOptions
{
    public string Client { get; init; } = "tawk-mcp";

    public string Version { get; init; } = "0.11.0";

    public string Origin { get; init; } = "mcp";

    /// <summary>
    /// What tells this instance apart from other tawk-mcp processes in tawk's list of connected agents:
    /// the folder it was started in and how it is run. Null sends none.
    /// </summary>
    public string? Label { get; init; }

    public int Protocol { get; init; } = 1;

    /// <summary>
    /// What this instance can do when tawk asks, by the names in <see cref="Core.TawkFeatures"/>: transcribe
    /// a voice note, write a summary. tawk asks only an agent that says it can. Empty sends none.
    /// </summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    public TimeSpan HelloTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a read may wait for its answer before tawk is treated as hung. Writes are exempt.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a request waits for a connect in progress when tawk is not connected.</summary>
    public TimeSpan ConnectWait { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Hand a write back to its caller as soon as tawk queues it for an answer, instead of waiting for the
    /// user. Only for an instance that may answer its own requests; off, writes wait as they always did.
    /// </summary>
    public bool ParkWaitingWrites { get; init; }
}
