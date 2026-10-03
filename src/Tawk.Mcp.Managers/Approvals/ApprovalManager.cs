using System.Globalization;
using System.Text;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers.Approvals;

public sealed class ApprovalManager(ITawkApprovals approvals, IAdminTokenSource tokens, TimeProvider clock) : IApprovalManager
{
    public string ListWaiting()
    {
        var waiting = approvals.TakeWaiting();
        if (waiting.Count == 0)
        {
            return "Nothing of yours is waiting for an answer in tawk.";
        }

        var now = clock.GetUtcNow();
        var text = new StringBuilder();
        foreach (var request in waiting)
        {
            var seconds = Math.Max(0, (int)(now - request.Since).TotalSeconds);
            text.Append(CultureInfo.InvariantCulture, $"- request {request.Id}: {request.Op}, ");
            text.Append(request.Outcome is null
                ? string.Create(CultureInfo.InvariantCulture, $"waiting for {seconds} s")
                : "answered in tawk meanwhile: " + request.Outcome);
            text.Append('\n');
        }

        return text.ToString().TrimEnd();
    }

    public async Task<string> ApproveAsync(string requestId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        var token = tokens.Read();
        if (token is null)
        {
            return "There is no admin token to show tawk, so the request still waits for the user in tawk. "
                + "tawk writes the token only while its access is admin (Settings > Automation > What they may do).";
        }

        try
        {
            var result = await approvals.ApproveAsync(requestId.Trim(), token, cancellationToken).ConfigureAwait(false);
            return "Approved by you as admin, and tawk carried it out. tawk recorded it in its automation log and told the user. Result: "
                + result.GetRawText();
        }
        catch (TawkControlException ex) when (ex.Code is ControlErrorCode.NotAllowed or ControlErrorCode.BadToken or ControlErrorCode.RateLimited)
        {
            return "tawk did not let you approve this, so it still waits for the user in tawk. tawk said: " + ex.Error.Message;
        }
        catch (TawkControlException ex) when (ex.Code is ControlErrorCode.NotFound)
        {
            return "No request with that id is waiting any more: it was answered in tawk, timed out, or the id is wrong. list_pending shows what waits.";
        }
    }
}
