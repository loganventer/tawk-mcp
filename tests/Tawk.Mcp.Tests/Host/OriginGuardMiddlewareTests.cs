using Microsoft.AspNetCore.Http;
using Tawk.Mcp.Host;

namespace Tawk.Mcp.Tests.Host;

public class OriginGuardMiddlewareTests
{
    [TestCase("http://localhost")]
    [TestCase("http://localhost:3000")]
    [TestCase("https://127.0.0.1:8765")]
    [TestCase("http://LOCALHOST:1")]
    public void Local_origins_are_allowed(string origin)
    {
        Assert.That(OriginGuardMiddleware.IsAllowed(origin), Is.True);
    }

    [TestCase("https://evil.example")]
    [TestCase("http://localhost.evil.example")]
    [TestCase("http://127.0.0.1.nip.io")]
    [TestCase("http://192.168.1.10:8765")]
    [TestCase("file://localhost")]
    [TestCase("null")]
    [TestCase("")]
    [TestCase("http://user@localhost")]
    public void Other_origins_are_refused(string origin)
    {
        Assert.That(OriginGuardMiddleware.IsAllowed(origin), Is.False);
    }

    [Test]
    public async Task A_foreign_origin_gets_403_and_goes_no_further()
    {
        var reached = false;
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = "https://evil.example";

        await new OriginGuardMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(403));
            Assert.That(reached, Is.False);
        });
    }

    [Test]
    public async Task No_origin_header_is_let_through_for_non_browser_clients()
    {
        var reached = false;

        await new OriginGuardMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        }).InvokeAsync(new DefaultHttpContext());

        Assert.That(reached, Is.True);
    }
}
