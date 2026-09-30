using Microsoft.AspNetCore.Http;

namespace Tawk.Mcp.Host;

/// <summary>
/// Rejects any request whose Origin is not http(s)://localhost or http(s)://127.0.0.1 on any port,
/// so a web page in the user's browser cannot talk to tawk-mcp.
/// </summary>
public sealed class OriginGuardMiddleware(RequestDelegate next)
{
    public static bool IsAllowed(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var schemeOk = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        var hostOk = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1";
        return schemeOk && hostOk && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.UserInfo);
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.Headers.TryGetValue("Origin", out var origin) && !IsAllowed(origin.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
