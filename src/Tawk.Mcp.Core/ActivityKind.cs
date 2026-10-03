namespace Tawk.Mcp.Core;

/// <summary>What happened to a message, other than arriving or being read.</summary>
public enum ActivityKind
{
    /// <summary>Someone reacted to a message the user sent, or took the reaction back.</summary>
    Reaction,

    /// <summary>Someone changed the words of a message they sent.</summary>
    Edited,

    /// <summary>Someone deleted a message they sent, for everyone.</summary>
    Deleted,

    /// <summary>A message the user scheduled went out; the id is the scheduled message's.</summary>
    ScheduledSent,
}
