namespace Tawk.Mcp.Managers;

/// <summary>Looks up whether the person in a chat is online.</summary>
public interface IPresenceManager
{
    /// <summary>Whether the person in the one-to-one chat <paramref name="chat"/> is online, or when they were last seen, as text for an agent.</summary>
    Task<string> OnlineStatusAsync(string chat, CancellationToken cancellationToken);
}
