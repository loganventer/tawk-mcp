using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class AppManager(ITawkControl control, IConfirmationGate gate, TawkControlOptions options) : IAppManager
{
    public const int MaxDescriptionWords = 10;

    public const int MaxDescriptionLength = 150;

    public async Task<string> AppStatusAsync(CancellationToken cancellationToken) =>
        WriteResults.Json(await control.RequestAsync("app_status", null, cancellationToken).ConfigureAwait(false));

    public async Task<string> VersionAsync(CancellationToken cancellationToken)
    {
        var mine = $"tawk-mcp {options.Version}";
        try
        {
            var hello = await control.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return $"{mine}\ntawk {hello.Tawk} (control protocol {hello.Protocol})";
        }
        catch (TawkControlException)
        {
            return $"{mine}\ntawk is not running, so its version is not known.";
        }
    }

    public async Task<string> ListAccountsAsync(CancellationToken cancellationToken)
    {
        var hello = await control.ConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!hello.MultiAccount)
        {
            return hello.Account is { } only
                ? $"One account: {only.Jid}{(only.Name is { Length: > 0 } name ? $" ({name})" : string.Empty)}, access {hello.Access}. This tawk does not take an account by name."
                : "tawk is not linked to WhatsApp yet.";
        }

        // Asked again each time: the user can open or close an account while this instance stays connected.
        var result = await control.RequestAsync("list_accounts", null, cancellationToken).ConfigureAwait(false);
        var accounts = result.TryGetProperty("accounts", out var list)
            ? ControlLineCodec.Deserialize<List<AccountSummary>>(list)
            : [];
        if (accounts.Count == 0)
        {
            return "No account is open to agents. The user chooses that in tawk under Settings, Account, Accounts.";
        }

        var defaultId = result.TryGetProperty("default", out var d) && d.TryGetInt32(out var id) ? id : 0;
        var lines = accounts.Select(a =>
            $"{a.Id}  {a.Label}  {(a.Jid.Length > 0 ? a.Jid : "not linked")}  access {a.Access}"
            + (a.Connected ? string.Empty : "  offline")
            + (a.Id == defaultId ? "  (default)" : string.Empty));
        return string.Join('\n', lines);
    }

    public async Task<string> DescribeSessionAsync(string description, CancellationToken cancellationToken)
    {
        var words = (description ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxDescriptionWords)
        {
            return $"That is {words.Length} words. Say it in at most {MaxDescriptionWords}: concise, and specific about the task and the project.";
        }

        var text = string.Join(' ', words);
        if (text.Length > MaxDescriptionLength)
        {
            text = text[..MaxDescriptionLength];
        }

        try
        {
            await control.RequestAsync("describe", new JsonObject { ["text"] = text }, cancellationToken).ConfigureAwait(false);
        }
        catch (TawkControlException ex) when (ex.Code == ControlErrorCode.BadRequest)
        {
            return "This tawk is too old to show what a session is doing. Carry on; nothing else is affected.";
        }

        return text.Length == 0 ? "tawk no longer shows a description for this session." : "tawk now shows that beside this session.";
    }

    public async Task<string> ReconnectAsync(WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await gate.ExecuteAsync("reconnect", null, context.Confirmation, context.OnApprovalWaiting, cancellationToken).ConfigureAwait(false),
            _ => "tawk is reconnecting to WhatsApp.");

    public async Task<string> DeclineCallAsync(WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await gate.ExecuteAsync("decline_call", null, context.Confirmation, context.OnApprovalWaiting, cancellationToken).ConfigureAwait(false),
            _ => "Declined the call.");
}
