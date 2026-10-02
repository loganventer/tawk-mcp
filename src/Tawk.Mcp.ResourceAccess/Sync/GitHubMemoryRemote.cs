using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>The memory database as a file in a GitHub repository, through the REST API. A version is the file's blob id.</summary>
public sealed class GitHubMemoryRemote(HttpClient http, SyncOptions options, string productVersion) : IMemoryRemote
{
    private const string Json = "application/vnd.github+json";
    private const string Raw = "application/vnd.github.raw+json";

    private string FilePath => string.Join('/', options.File.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    public async Task<string?> HeadAsync(CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"contents/{FilePath}?ref={Uri.EscapeDataString(options.Branch)}", Json);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureAsync(response, "read", cancellationToken).ConfigureAwait(false);
        return await ShaAsync(response, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadAsync(string version, string destination, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"git/blobs/{Uri.EscapeDataString(version)}", Raw);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureAsync(response, "download", cancellationToken).ConfigureAwait(false);
        var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (file.ConfigureAwait(false))
        {
            await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string?> PushAsync(string file, string? baseVersion, string message, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["message"] = message,
            ["branch"] = options.Branch,
            ["content"] = Convert.ToBase64String(await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false)),
        };
        if (baseVersion is not null)
        {
            body["sha"] = baseVersion;
        }

        using var request = Request(HttpMethod.Put, $"contents/{FilePath}", Json);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        // GitHub answers 409 when the file moved on, and 422 when it exists but no version was named.
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            return null;
        }

        await EnsureAsync(response, "write", cancellationToken).ConfigureAwait(false);
        return await ShaAsync(response, "content", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ShaAsync(HttpResponseMessage response, string? inside, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
                var holder = inside is null ? document.RootElement : document.RootElement.GetProperty(inside);
                return holder.GetProperty("sha").GetString() ?? throw new MemoryException("GitHub's answer named no version of the memory file.");
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new MemoryException("GitHub's answer about the memory file could not be read.", ex);
        }
    }

    private static async Task EnsureAsync(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var hint = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "The token was refused.",
            HttpStatusCode.Forbidden => "The token may not do that; it needs contents read and write on the repository.",
            HttpStatusCode.NotFound => "The repository or branch was not found, or the token cannot see it.",
            _ => (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)) is { Length: > 0 } text
                ? text[..Math.Min(text.Length, 200)]
                : string.Empty,
        };
        throw new MemoryException($"Could not {what} the remote memory (HTTP {(int)response.StatusCode}). {hint}".TrimEnd());
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string accept)
    {
        var request = new HttpRequestMessage(method, new Uri(options.Api, $"repos/{options.Repository}/{path}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        request.Headers.Accept.ParseAdd(accept);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("tawk-mcp", productVersion));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new MemoryException("Could not reach the remote memory: " + ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MemoryException("The remote memory did not answer in time.", ex);
        }
    }
}
