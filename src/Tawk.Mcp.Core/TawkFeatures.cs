namespace Tawk.Mcp.Core;

/// <summary>The names tawk gives in its hello for what it can do.</summary>
public static class TawkFeatures
{
    /// <summary>tawk keeps voice note transcripts: <c>set_transcript</c> and <c>get_transcript</c> are there.</summary>
    public const string Transcripts = "transcripts";

    /// <summary>tawk keeps TL;DR summaries: <c>set_summary</c> and <c>get_summary</c> are there, and it asks for them with <c>summary_wanted</c>.</summary>
    public const string Summaries = "summaries";

    /// <summary>tawk has an owner's chat: what the user types there arrives as <c>owner_message</c>, and an answer sent there needs no approval.</summary>
    public const string OwnerChat = "owner_chat";
}
