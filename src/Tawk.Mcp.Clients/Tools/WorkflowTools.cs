using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class WorkflowTools(WorkflowOptions options)
{
    [McpServerTool(Name = TawkWorkflow.ToolName, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show the workflow for keeping tawk-mcp's memory current: what to record about people, relations and the user's voice, and how. "
        + "tawk-mcp also adds it to a tool result every so often; follow it when it appears. It includes the user's own standing instructions.")]
    public Task<CallToolResult> GetWorkflowAsync() => Task.FromResult(ToolResults.Text(TawkWorkflow.For(options)));
}
