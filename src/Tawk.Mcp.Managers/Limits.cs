namespace Tawk.Mcp.Managers;

internal static class Limits
{
    public static int? Clamp(int? value, int max) => value is { } v ? Math.Clamp(v, 1, max) : null;
}
