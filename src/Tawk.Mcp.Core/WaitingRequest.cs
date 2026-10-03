namespace Tawk.Mcp.Core;

/// <summary>
/// A write of this instance that tawk queued for an answer. <see cref="Outcome"/> is null while it still
/// waits, and says what became of it once tawk answered without this instance approving it.
/// </summary>
public sealed record WaitingRequest(string Id, string Op, DateTimeOffset Since, string? Outcome);
