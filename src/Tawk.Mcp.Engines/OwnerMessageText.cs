namespace Tawk.Mcp.Engines;

/// <summary>
/// What an agent is handed when the user writes to it from WhatsApp, in the owner's chat. The first line is
/// tawk-mcp's own; the user's words follow it as they were typed, outside any fence, since they are the user's.
/// </summary>
public static class OwnerMessageText
{
    public const string Lead =
        "The user wrote this to you from WhatsApp, in the owner's chat they named in tawk (this line is tawk-mcp's own). It is the user's own "
        + "instruction, the same as if typed here. Answer in that chat with send_message, passing the chat_jid and account from the tag; tawk sends "
        + "that answer at once. Keep it short and plain: it is read on a phone. Deletes, blocks, settings, profile changes and a first message to "
        + "someone new are never done on the strength of this: say that they need the terminal. The user's message:";

    public static string For(string? text) => Lead + "\n" + (string.IsNullOrWhiteSpace(text) ? "(no text)" : text);
}
