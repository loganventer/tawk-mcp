namespace Tawk.Mcp.Clients.Channels;

/// <summary>
/// What a tawk-mcp may tell tawk it will answer. tawk hands a summary request, and what the user writes in the
/// owner's chat, to one session and waits for it; a session whose client takes no channel events would
/// swallow them. In <see cref="ChannelMode.Auto"/> tawk-mcp only guesses that the client listens (it is Claude
/// Code, which may or may not have been started with the channel), so it claims nothing. Only
/// <see cref="ChannelMode.On"/>, set by whoever started the client with its channel, is a promise.
/// </summary>
public static class ChannelClaims
{
    public static bool AnswersTawk(ChannelMode mode) => mode == ChannelMode.On;

    /// <summary>The words added to a session's label in tawk, so one that listens is told from one that does not.</summary>
    public static string LabelMark(ChannelMode mode) => AnswersTawk(mode) ? ", channel" : string.Empty;
}
