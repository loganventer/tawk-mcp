namespace Tawk.Mcp.Core;

/// <summary>How a write reaches the user: a way to ask them directly, and a hook for tawk's approval wait.</summary>
public sealed record WriteContext(IUserConfirmation? Confirmation, Action? OnApprovalWaiting)
{
    public static WriteContext None { get; } = new(null, null);
}
