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

    /// <summary>tawk keeps the user's labels on chats: <c>list_labels</c> and <c>set_label</c> are there.</summary>
    public const string Labels = "labels";

    /// <summary>tawk keeps chats put aside with a reminder: <c>list_reminders</c>, <c>set_reminder</c> and <c>cancel_reminder</c> are there.</summary>
    public const string Reminders = "reminders";

    /// <summary>tawk can say which chats await a reply: <c>awaiting_replies</c> is there.</summary>
    public const string AwaitingReplies = "awaiting_replies";
}
