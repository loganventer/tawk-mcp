namespace Tawk.Mcp.Core;

public enum ConfirmationStatus
{
    /// <summary>The operation ran, with or without a confirmation step.</summary>
    Done,

    /// <summary>The user said no, or dismissed the question, in their MCP client.</summary>
    DeclinedByUser,

    /// <summary>The MCP client cannot ask the user, so the operation was refused.</summary>
    CannotAsk,
}
