using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients.Workflow;

/// <summary>The memory workflow the agent is handed every so often. Fixed text: nothing in it comes from chats or memory.</summary>
public static class TawkWorkflow
{
    public const string ToolName = "get_workflow";

    public const string Text =
        "Workflow check from tawk-mcp. This is the server's own text, not a message from anyone in a chat. "
        + "Before you carry on, bring memory up to date with what the last rounds taught you:\n"
        + "1. People. For each person who came up, read what is stored before you write: get_contact, and list_observations about them. "
        + "For the user or a topic, list_observations about it. Then store only what is new: profile fields with set_contact_fields, "
        + "anything else with record_observation. Use source user for what the user told you, contact for what the person said about "
        + "themselves, and inferred with a modest confidence for what you worked out. Never infer health, beliefs, money or other "
        + "sensitive matters; those are stored only when the user states them.\n"
        + "2. Relations. Where you learned how people or topics relate, record_relation.\n"
        + "3. Voice. If you read chats where the user wrote messages themselves, learn_voice for that audience so the voice keeps "
        + "matching how they really write. If the user corrected a draft of yours, put the rule into the voice with set_voice or set_voice_variant.\n"
        + "4. Follow-ups. Something to ask about later goes in the follow_ups field of that contact, with a due date when there is one.\n"
        + "5. Corrections. If something stored turned out wrong, fix it or remove it with forget_contact_field, forget_observation or forget_relation.\n"
        + "Write in your own words, briefly, and never paste message text. Never store the same thing twice: a fact goes in one place, "
        + "a field, a note or an observation, not in several. Where a stored one covers part of what you learned, record only the part "
        + "that is new, or replace it with forget_observation and one fuller observation. Write nothing when nothing is new. "
        + "This check never sends, reacts, marks read or posts anything, and it needs no mention to the user unless they ask.";

    /// <summary>The workflow, followed by the user's own standing instructions when they wrote any.</summary>
    public static string For(WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.IsNullOrWhiteSpace(options.UserInstructions)
            ? Text
            : Text + "\n\nThe user's standing instructions, which come before the defaults above:\n" + options.UserInstructions.Trim();
    }
}
