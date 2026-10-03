namespace Tawk.Mcp.Core;

/// <summary>
/// Which of the user's accounts the work in progress is for. A tool call opens the scope with the account
/// it was given, and every request to tawk made inside it names that account.
/// </summary>
public interface IAccountScope
{
    /// <summary>The account's label or id, or null for tawk's default account.</summary>
    string? Current { get; }

    /// <summary>Makes <paramref name="account"/> the current account until the result is disposed.</summary>
    IDisposable Use(string? account);
}
