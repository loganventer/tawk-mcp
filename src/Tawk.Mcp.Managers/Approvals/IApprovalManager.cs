namespace Tawk.Mcp.Managers.Approvals;

/// <summary>This instance's own writes that wait for an answer in tawk, and answering one as admin.</summary>
public interface IApprovalManager
{
    string ListWaiting();

    Task<string> ApproveAsync(string requestId, CancellationToken cancellationToken);
}
