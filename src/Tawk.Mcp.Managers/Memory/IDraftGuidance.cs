namespace Tawk.Mcp.Managers.Memory;

/// <summary>What the draft_reply prompt adds about how to write to a chat. Empty when memory is off or knows nothing.</summary>
public interface IDraftGuidance
{
    Task<string> ForChatAsync(string chat, CancellationToken cancellationToken);
}
