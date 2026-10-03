using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class FakeAdminTokenSource(string? token) : IAdminTokenSource
{
    public string? Read() => token;
}
