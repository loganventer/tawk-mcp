namespace Tawk.Mcp.Clients.Resources;

/// <summary>
/// What a chat resource uri points at. <see cref="Account"/> is the label or id the uri names, or null
/// for the default account; <see cref="Jid"/> is the chat, or null for the chat list.
/// </summary>
public sealed record ResourceAddress(string? Account, string? Jid);
