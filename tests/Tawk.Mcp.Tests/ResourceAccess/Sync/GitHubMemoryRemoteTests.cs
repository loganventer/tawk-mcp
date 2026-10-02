using System.Net;
using System.Text;
using System.Text.Json;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.ResourceAccess.Sync;

namespace Tawk.Mcp.Tests.ResourceAccess.Sync;

public class GitHubMemoryRemoteTests
{
    private readonly List<(HttpMethod Method, string Url, string? Body, string? Accept, string? Authorization)> _requests = [];
    private Func<HttpRequestMessage, HttpResponseMessage> _answer = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    [Test]
    public async Task The_head_is_the_files_blob_on_the_branch_or_nothing_when_there_is_no_file()
    {
        using var http = Client();
        var remote = Remote(http);
        var none = await remote.HeadAsync(default);
        _answer = _ => Json(HttpStatusCode.OK, """{"sha":"abc123","size":2000000,"content":""}""");
        var head = await remote.HeadAsync(default);

        Assert.Multiple(() =>
        {
            Assert.That(none, Is.Null);
            Assert.That(head, Is.EqualTo("abc123"));
            Assert.That(_requests[1].Url, Is.EqualTo("https://api.github.test/v3/repos/someone/memory/contents/data/my%20memory.db?ref=main"));
            Assert.That(_requests[1].Authorization, Is.EqualTo("Bearer secret"));
        });
    }

    [Test]
    public async Task A_download_asks_for_exactly_that_version_as_raw_bytes()
    {
        using var http = Client();
        _answer = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        var path = Path.Combine(Path.GetTempPath(), "tawk-remote-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Remote(http).DownloadAsync("abc123", path, default);

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(_requests[0].Url, Is.EqualTo("https://api.github.test/v3/repos/someone/memory/git/blobs/abc123"));
                Assert.That(_requests[0].Accept, Is.EqualTo("application/vnd.github.raw+json"));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task A_push_names_the_version_it_replaces_and_a_lost_race_is_not_an_error()
    {
        using var http = Client();
        var remote = Remote(http);
        var path = Path.Combine(Path.GetTempPath(), "tawk-remote-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, [9, 8, 7]);
        try
        {
            _answer = _ => Json(HttpStatusCode.OK, """{"content":{"sha":"def456"},"commit":{"sha":"c1"}}""");
            var pushed = await remote.PushAsync(path, "abc123", "Sync memory", default);
            _answer = _ => Json(HttpStatusCode.Conflict, """{"message":"is at def456 but expected abc123"}""");
            var lost = await remote.PushAsync(path, "abc123", "Sync memory", default);
            using var body = JsonDocument.Parse(_requests[0].Body!);

            Assert.Multiple(() =>
            {
                Assert.That(pushed, Is.EqualTo("def456"));
                Assert.That(lost, Is.Null);
                Assert.That(_requests[0].Method, Is.EqualTo(HttpMethod.Put));
                Assert.That(body.RootElement.GetProperty("sha").GetString(), Is.EqualTo("abc123"));
                Assert.That(body.RootElement.GetProperty("branch").GetString(), Is.EqualTo("main"));
                Assert.That(body.RootElement.GetProperty("content").GetString(), Is.EqualTo(Convert.ToBase64String([9, 8, 7])));
                Assert.That(body.RootElement.GetProperty("message").GetString(), Is.EqualTo("Sync memory"));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void A_refused_token_and_a_dead_network_are_said_plainly_without_the_token()
    {
        using var http = Client();
        var remote = Remote(http);
        _answer = _ => Json(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""");
        var refused = Assert.ThrowsAsync<MemoryException>(() => remote.HeadAsync(default));
        _answer = _ => throw new HttpRequestException("no route to host");
        var dead = Assert.ThrowsAsync<MemoryException>(() => remote.HeadAsync(default));

        Assert.Multiple(() =>
        {
            Assert.That(refused!.Message, Does.Contain("HTTP 401").And.Contain("token was refused").And.Not.Contain("secret"));
            Assert.That(dead!.Message, Does.Contain("Could not reach the remote memory"));
        });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static GitHubMemoryRemote Remote(HttpClient http) => new(
        http, new SyncOptions { Token = "secret", Repository = "someone/memory", File = "data/my memory.db", Api = new Uri("https://api.github.test/v3/") }, "1.2.3");

    private HttpClient Client() => new(new Handler(this));

    private sealed class Handler(GitHubMemoryRemoteTests test) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            test._requests.Add((
                request.Method, request.RequestUri!.AbsoluteUri, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Accept.ToString(), request.Headers.Authorization?.ToString()));
            return test._answer(request);
        }
    }
}
