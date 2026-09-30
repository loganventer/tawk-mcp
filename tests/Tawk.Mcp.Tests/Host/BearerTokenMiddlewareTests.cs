using Microsoft.AspNetCore.Http;
using Tawk.Mcp.Host;

namespace Tawk.Mcp.Tests.Host;

public class BearerTokenMiddlewareTests
{
    private const string Token = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

    private static async Task<(int Status, bool Reached)> Send(string path, string? authorization)
    {
        var reached = false;
        var middleware = new BearerTokenMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        }, new FixedTokenStore(Token));
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, reached);
    }

    [TestCase("/mcp")]
    [TestCase("/events")]
    public async Task Requests_without_a_token_are_refused(string path)
    {
        var (status, reached) = await Send(path, null);

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(401));
            Assert.That(reached, Is.False);
        });
    }

    [TestCase("Bearer wrong")]
    [TestCase("Bearer " + Token + "x")]
    [TestCase("Basic " + Token)]
    [TestCase("Bearer")]
    public async Task Wrong_tokens_are_refused(string header)
    {
        Assert.That((await Send("/events", header)).Status, Is.EqualTo(401));
    }

    [TestCase("Bearer " + Token)]
    [TestCase("bearer  " + Token + " ")]
    public async Task The_right_token_goes_through(string header)
    {
        Assert.That((await Send("/events", header)).Reached, Is.True);
    }

    [Test]
    public async Task The_health_check_needs_no_token()
    {
        Assert.That((await Send("/healthz", null)).Reached, Is.True);
    }
}
