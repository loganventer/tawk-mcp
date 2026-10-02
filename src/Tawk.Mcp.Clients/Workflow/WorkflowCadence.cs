using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients.Workflow;

public sealed class WorkflowCadence(WorkflowOptions options) : IWorkflowCadence
{
    private int _rounds;

    public void Round() => Interlocked.Increment(ref _rounds);

    public bool TakeDue()
    {
        if (!options.Enabled)
        {
            return false;
        }

        // Only the caller that moves the count back to zero gets to hand the workflow out.
        while (true)
        {
            var rounds = Volatile.Read(ref _rounds);
            if (rounds < options.Every)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _rounds, 0, rounds) == rounds)
            {
                return true;
            }
        }
    }

    public void Reset() => Interlocked.Exchange(ref _rounds, 0);
}
