using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients.Workflow;

/// <summary>A tool call filter that counts each call as a round and, when the workflow is due, adds it to the result.</summary>
public static class WorkflowReminder
{
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Filter(IWorkflowCadence cadence, WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(cadence);
        ArgumentNullException.ThrowIfNull(options);
        return next => async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);
            if (context.Params?.Name == TawkWorkflow.ToolName)
            {
                cadence.Reset();
                return result;
            }

            cadence.Round();
            if (result.IsError != true && cadence.TakeDue())
            {
                result.Content = [.. result.Content, new TextContentBlock { Text = TawkWorkflow.For(options) }];
            }

            return result;
        };
    }
}
