namespace Tawk.Mcp.ResourceAccess;

public sealed record TawkControlOptions
{
    public string Client { get; init; } = "tawk-mcp";

    public string Version { get; init; } = "0.1.0";

    public string Origin { get; init; } = "mcp";

    public int Protocol { get; init; } = 1;

    public TimeSpan HelloTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a read may wait for its answer before tawk is treated as hung. Writes are exempt.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a request waits for a connect in progress when tawk is not connected.</summary>
    public TimeSpan ConnectWait { get; init; } = TimeSpan.FromSeconds(2);
}
