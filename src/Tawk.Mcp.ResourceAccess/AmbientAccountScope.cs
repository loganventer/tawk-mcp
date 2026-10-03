using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>Keeps the current account with the flow of the call, so calls running side by side each have their own.</summary>
public sealed class AmbientAccountScope : IAccountScope
{
    private readonly AsyncLocal<string?> _current = new();

    public string? Current => _current.Value;

    public IDisposable Use(string? account)
    {
        var before = _current.Value;
        _current.Value = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        return new Restore(this, before);
    }

    private sealed class Restore(AmbientAccountScope scope, string? before) : IDisposable
    {
        public void Dispose() => scope._current.Value = before;
    }
}
