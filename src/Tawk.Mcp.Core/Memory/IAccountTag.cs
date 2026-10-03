namespace Tawk.Mcp.Core.Memory;

/// <summary>
/// Says which of the user's accounts something is being remembered through. The tag is the account's own
/// jid, which stays the same when the user renames the account and can be read without asking tawk.
/// </summary>
public interface IAccountTag
{
    /// <summary>The jid of the account the work in progress is for, or null when it cannot be told.</summary>
    Task<string?> CurrentAsync(CancellationToken cancellationToken);
}
