using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Tawk.Mcp.Host;

/// <summary>Requires "Authorization: Bearer &lt;token&gt;" on every request except the health check.</summary>
public sealed class BearerTokenMiddleware(RequestDelegate next, ITokenStore tokens)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(tokens.GetOrCreate());

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.Path.Equals(HealthEndpoint.Path, StringComparison.Ordinal) || Authorised(context.Request))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    private bool Authorised(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string scheme = "Bearer ";
        if (!header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = Encoding.UTF8.GetBytes(header[scheme.Length..].Trim());
        return CryptographicOperations.FixedTimeEquals(presented, _expected);
    }
}
