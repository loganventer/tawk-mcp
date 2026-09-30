namespace Tawk.Mcp.Managers.Memory;

/// <summary>Used when memory is off: the prompt stays as it always was.</summary>
public sealed class NoDraftGuidance : IDraftGuidance
{
    public Task<string> ForChatAsync(string chat, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
}
