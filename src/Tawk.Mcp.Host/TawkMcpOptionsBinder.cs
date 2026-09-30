using System.Globalization;
using Tawk.Mcp.Clients.Channels;

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
                "--version" or "-v" => options with { Command = HostCommand.Version },
                "--help" or "-h" => options with { Command = HostCommand.Help },
                "--http" => options with { Transport = TransportKind.Http },
                "--stdio" => options with { Transport = TransportKind.Stdio },
                "--port" => options with { Port = Int(arg, Next(), 1, 65535, ref error) },
                "--bind" => options with { Bind = Next() },
                "--socket" => options with { SocketPath = Next() },
                "--token-file" => options with { TokenFile = Next() },
                "--backoff-initial-ms" => options with { BackoffInitialMs = Int(arg, Next(), 1, 600_000, ref error) },
                "--backoff-max-ms" => options with { BackoffMaxMs = Int(arg, Next(), 1, 3_600_000, ref error) },
                "--breaker-threshold" => options with { BreakerThreshold = Int(arg, Next(), 1, 1000, ref error) },
                "--breaker-cooldown-s" => options with { BreakerCooldownS = Int(arg, Next(), 0, 86_400, ref error) },
                "--request-timeout-s" => options with { RequestTimeoutS = Int(arg, Next(), 1, 600, ref error) },
                "--channel" => options with { Channel = Channel(arg, Next(), ref error) },
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

    private static TawkMcpOptions Unknown(TawkMcpOptions options, string arg, ref string? error) =>
        Fail(options, $"Unknown argument: {arg}. Try tawk-mcp --help.", ref error);

    private static TawkMcpOptions Fail(TawkMcpOptions options, string message, ref string? error)
    {
        error ??= message;
        return options;
    }
}
