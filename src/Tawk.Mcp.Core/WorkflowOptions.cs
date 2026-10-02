namespace Tawk.Mcp.Core;

/// <summary>
/// How often the connected agent is handed the memory workflow, in rounds (a tool call or an incoming
/// message each count as one), and the user's own standing instructions, if they wrote any.
/// </summary>
public sealed record WorkflowOptions(int Every, string? UserInstructions)
{
    public const int DefaultEvery = 20;

    public bool Enabled => Every > 0;
}
