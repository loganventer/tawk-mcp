namespace Tawk.Mcp.Clients.Workflow;

/// <summary>Counts rounds, and says when the agent is due the workflow again.</summary>
public interface IWorkflowCadence
{
    /// <summary>One more round: a tool call, or a message that arrived for the agent.</summary>
    void Round();

    /// <summary>True once per interval, when enough rounds have passed. Taking it starts the count again.</summary>
    bool TakeDue();

    /// <summary>Starts the count again, as when the agent has just asked for the workflow itself.</summary>
    void Reset();
}
