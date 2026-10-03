using System.Globalization;
using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Host;

/// <summary>Reads options from the environment first, then lets command-line flags override them.</summary>
public static class TawkMcpOptionsBinder
{
    public static TawkMcpOptions Bind(IReadOnlyList<string> args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        var options = new TawkMcpOptions();
        string? error = null;

        options = Apply(options, environment, ref error);
        for (var i = 0; i < args.Count && error is null; i++)
        {
            var arg = args[i];
            string Next()
            {
                if (i + 1 >= args.Count)
                {
                    error = $"{arg} needs a value.";
                    return string.Empty;
                }

                return args[++i];
            }

            options = arg switch
            {
                "print-token" => options with { Command = HostCommand.PrintToken },
                "healthcheck" => options with { Command = HostCommand.Healthcheck },
                "export-okf" => options with { Command = HostCommand.ExportOkf, BundlePath = Next() },
                "import-okf" => options with { Command = HostCommand.ImportOkf, BundlePath = Next() },
                "sync" => options with { Command = HostCommand.Sync },
                "--include-sensitive" => options with { IncludeSensitive = true },
                "--sync-repo" => options with { SyncRepository = Repository(arg, Next(), ref error) },
                "--sync-branch" => options with { SyncBranch = Next() },
                "--sync-file" => options with { SyncFile = SyncFile(arg, Next(), ref error) },
                "--sync-api" => options with { SyncApi = SyncApi(arg, Next(), ref error) },
                "--sync-interval-minutes" => options with { SyncIntervalMinutes = Int(arg, Next(), 1, 10_080, ref error) },
                "--workflow-every" => options with { WorkflowEvery = Int(arg, Next(), 0, 10_000, ref error) },
                "--instructions-file" => options with { InstructionsFile = Next() },
                "--admin-token-file" => options with { AdminTokenFile = Next() },
                "--version" or "-v" => options with { Command = HostCommand.Version },
                "--help" or "-h" => options with { Command = HostCommand.Help },
                "--http" => options with { Transport = TransportKind.Http },
                "--stdio" => options with { Transport = TransportKind.Stdio },
                "--port" => options with { Port = Int(arg, Next(), 1, 65535, ref error) },
                "--bind" => options with { Bind = Next() },
                "--socket" => options with { SocketPath = Next() },
                "--token-file" => options with { TokenFile = Next() },
                "--data-file" => options with { DataFile = Next() },
                "--memory" => options with { Memory = Memory(arg, Next(), ref error) },
                "--backoff-initial-ms" => options with { BackoffInitialMs = Int(arg, Next(), 1, 600_000, ref error) },
                "--backoff-max-ms" => options with { BackoffMaxMs = Int(arg, Next(), 1, 3_600_000, ref error) },
                "--breaker-threshold" => options with { BreakerThreshold = Int(arg, Next(), 1, 1000, ref error) },
                "--breaker-cooldown-s" => options with { BreakerCooldownS = Int(arg, Next(), 0, 86_400, ref error) },
                "--request-timeout-s" => options with { RequestTimeoutS = Int(arg, Next(), 1, 600, ref error) },
                "--channel" => options with { Channel = Channel(arg, Next(), ref error) },
                "--channel-own" => options with { ChannelOwn = OnOff(arg, Next(), ref error) },
                "--schedule-jitter-s" => options with { ScheduleJitterS = Int(arg, Next(), 0, 3600, ref error) },
                _ => Unknown(options, arg, ref error),
            };
        }

        if (error is null && options.BackoffMaxMs < options.BackoffInitialMs)
        {
            error = "The backoff maximum must not be below the initial backoff.";
        }

        return options with { Error = error };
    }

    private static TawkMcpOptions Apply(TawkMcpOptions options, Func<string, string?> env, ref string? error)
    {
        if (env("TAWKMCP_TRANSPORT") is { Length: > 0 } transport)
        {
            options = transport.ToUpperInvariant() switch
            {
                "HTTP" => options with { Transport = TransportKind.Http },
                "STDIO" => options with { Transport = TransportKind.Stdio },
                _ => Fail(options, $"TAWKMCP_TRANSPORT must be stdio or http, not {transport}.", ref error),
            };
        }

        if (env("TAWKMCP_PORT") is { Length: > 0 } port)
        {
            options = options with { Port = Int("TAWKMCP_PORT", port, 1, 65535, ref error) };
        }

        if (env("TAWKMCP_BIND") is { Length: > 0 } bind)
        {
            options = options with { Bind = bind };
        }

        if (env("TAWKMCP_TOKEN_FILE") is { Length: > 0 } tokenFile)
        {
            options = options with { TokenFile = tokenFile };
        }

        if (env("TAWKMCP_DATA_FILE") is { Length: > 0 } dataFile)
        {
            options = options with { DataFile = dataFile };
        }

        if (env("TAWKMCP_MEMORY") is { Length: > 0 } memory)
        {
            options = options with { Memory = Memory("TAWKMCP_MEMORY", memory, ref error) };
        }

        if (env("TAWKMCP_TOKEN") is { Length: > 0 } token)
        {
            options = options with { Token = token };
        }

        if (env("TAWKMCP_BACKOFF_INITIAL_MS") is { Length: > 0 } initial)
        {
            options = options with { BackoffInitialMs = Int("TAWKMCP_BACKOFF_INITIAL_MS", initial, 1, 600_000, ref error) };
        }

        if (env("TAWKMCP_BACKOFF_MAX_MS") is { Length: > 0 } max)
        {
            options = options with { BackoffMaxMs = Int("TAWKMCP_BACKOFF_MAX_MS", max, 1, 3_600_000, ref error) };
        }

        if (env("TAWKMCP_BREAKER_THRESHOLD") is { Length: > 0 } threshold)
        {
            options = options with { BreakerThreshold = Int("TAWKMCP_BREAKER_THRESHOLD", threshold, 1, 1000, ref error) };
        }

        if (env("TAWKMCP_BREAKER_COOLDOWN_S") is { Length: > 0 } cooldown)
        {
            options = options with { BreakerCooldownS = Int("TAWKMCP_BREAKER_COOLDOWN_S", cooldown, 0, 86_400, ref error) };
        }

        if (env("TAWKMCP_REQUEST_TIMEOUT_S") is { Length: > 0 } timeout)
        {
            options = options with { RequestTimeoutS = Int("TAWKMCP_REQUEST_TIMEOUT_S", timeout, 1, 600, ref error) };
        }

        if (env("TAWKMCP_SCHEDULE_JITTER_S") is { Length: > 0 } jitter)
        {
            options = options with { ScheduleJitterS = Int("TAWKMCP_SCHEDULE_JITTER_S", jitter, 0, 3600, ref error) };
        }

        // The sync token is read from the environment only, so it never shows in a process list.
        if (env("TAWKMCP_SYNC_TOKEN") is { Length: > 0 } syncToken)
        {
            options = options with { SyncToken = syncToken };
        }

        if (env("TAWKMCP_SYNC_REPO") is { Length: > 0 } repository)
        {
            options = options with { SyncRepository = Repository("TAWKMCP_SYNC_REPO", repository, ref error) };
        }

        if (env("TAWKMCP_SYNC_BRANCH") is { Length: > 0 } branch)
        {
            options = options with { SyncBranch = branch };
        }

        if (env("TAWKMCP_SYNC_FILE") is { Length: > 0 } syncFile)
        {
            options = options with { SyncFile = SyncFile("TAWKMCP_SYNC_FILE", syncFile, ref error) };
        }

        if (env("TAWKMCP_SYNC_API") is { Length: > 0 } syncApi)
        {
            options = options with { SyncApi = SyncApi("TAWKMCP_SYNC_API", syncApi, ref error) };
        }

        if (env("TAWKMCP_SYNC_INTERVAL_MINUTES") is { Length: > 0 } interval)
        {
            options = options with { SyncIntervalMinutes = Int("TAWKMCP_SYNC_INTERVAL_MINUTES", interval, 1, 10_080, ref error) };
        }

        if (env("TAWKMCP_WORKFLOW_EVERY") is { Length: > 0 } every)
        {
            options = options with { WorkflowEvery = Int("TAWKMCP_WORKFLOW_EVERY", every, 0, 10_000, ref error) };
        }

        if (env("TAWKMCP_ADMIN_TOKEN_FILE") is { Length: > 0 } adminToken)
        {
            options = options with { AdminTokenFile = adminToken };
        }

        if (env("TAWKMCP_INSTRUCTIONS_FILE") is { Length: > 0 } instructions)
        {
            options = options with { InstructionsFile = instructions };
        }

        if (env("TAWKMCP_CHANNEL_OWN") is { Length: > 0 } channelOwn)
        {
            options = options with { ChannelOwn = OnOff("TAWKMCP_CHANNEL_OWN", channelOwn, ref error) };
        }

        if (env("TAWKMCP_CHANNEL") is { Length: > 0 } channel)
        {
            options = options with { Channel = Channel("TAWKMCP_CHANNEL", channel, ref error) };
        }

        return options;
    }

    private static int Int(string name, string value, int min, int max, ref string? error)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max)
        {
            return n;
        }

        error ??= $"{name} must be a whole number from {min} to {max}, not {value}.";
        return min;
    }

    private static string Repository(string name, string value, ref string? error)
    {
        var parts = value.Split('/');
        if (parts.Length != 2 || parts.Any(part => part.Length == 0 || part.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))))
        {
            error ??= $"{name} must be a GitHub repository as owner/name, not {value}.";
        }

        return value;
    }

    private static string SyncFile(string name, string value, ref string? error)
    {
        if (value.StartsWith('/') || value.EndsWith('/') || value.Contains('\\', StringComparison.Ordinal)
            || value.Split('/').Any(part => part is "" or "." or ".."))
        {
            error ??= $"{name} must be a file path inside the repository, such as memory.db or data/memory.db, not {value}.";
        }

        return value;
    }

    // The token goes to this address, so it has to be an encrypted one.
    private static string SyncApi(string name, string value, ref string? error)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            error ??= $"{name} must be an https address, such as https://api.github.com, not {value}.";
        }

        return value.EndsWith('/') ? value : value + "/";
    }

    private static bool OnOff(string name, string value, ref string? error)
    {
        switch (value.ToUpperInvariant())
        {
            case "ON":
                return true;
            case "OFF":
                return false;
            default:
                error ??= $"{name} must be on or off, not {value}.";
                return false;
        }
    }

    private static ChannelMode Channel(string name, string value, ref string? error)
    {
        switch (value.ToUpperInvariant())
        {
            case "AUTO":
                return ChannelMode.Auto;
            case "ON":
                return ChannelMode.On;
            case "OFF":
                return ChannelMode.Off;
            default:
                error ??= $"{name} must be auto, on or off, not {value}.";
                return ChannelMode.Off;
        }
    }

    private static MemoryMode Memory(string name, string value, ref string? error)
    {
        switch (value.ToUpperInvariant())
        {
            case "WRITE":
                return MemoryMode.Write;
            case "READ":
                return MemoryMode.Read;
            case "OFF":
                return MemoryMode.Off;
            default:
                error ??= $"{name} must be write, read or off, not {value}.";
                return MemoryMode.Off;
        }
    }

    private static TawkMcpOptions Unknown(TawkMcpOptions options, string arg, ref string? error) =>
        Fail(options, $"Unknown argument: {arg}. Try tawk-mcp --help.", ref error);

    private static TawkMcpOptions Fail(TawkMcpOptions options, string message, ref string? error)
    {
        error ??= message;
        return options;
    }
}
