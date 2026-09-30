using System.Globalization;

namespace Tawk.Mcp.Host;

/// <summary>`tawk-mcp healthcheck`: asks the running HTTP server for /healthz. Used by Docker, which has no curl.</summary>
public static class HealthCheckCommand
{
    public static async Task<int> RunAsync(TawkMcpOptions options, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        var host = options.Bind is "0.0.0.0" or "::" or "[::]" ? "127.0.0.1" : options.Bind;
        var url = string.Create(CultureInfo.InvariantCulture, $"http://{host}:{options.Port}{HealthEndpoint.Path}");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await client.GetAsync(new Uri(url)).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            await output.WriteLineAsync(body).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await output.WriteLineAsync($"tawk-mcp is not answering at {url}: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }
}
