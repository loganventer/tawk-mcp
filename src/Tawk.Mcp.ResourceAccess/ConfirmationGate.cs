using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

public sealed class ConfirmationGate(ITawkControl control) : IConfirmationGate
{
    public async Task<ConfirmationOutcome> ExecuteAsync(
        string op,
        JsonObject? args,
        IUserConfirmation? confirmation,
        Action? onApprovalWaiting,
        CancellationToken cancellationToken)
    {
        var first = await control.RequestAsync(op, args, onApprovalWaiting, cancellationToken).ConfigureAwait(false);
        if (!NeedsConfirmation(first, out var token, out var summary))
        {
            return new ConfirmationOutcome(ConfirmationStatus.Done, first);
        }

        var confirmed = false;
        try
        {
            var answer = confirmation is null
                ? ConfirmationAnswer.NotSupported
                : await confirmation.AskAsync(summary, cancellationToken).ConfigureAwait(false);
            if (answer != ConfirmationAnswer.Accepted)
            {
                return new ConfirmationOutcome(
                    answer == ConfirmationAnswer.NotSupported ? ConfirmationStatus.CannotAsk : ConfirmationStatus.DeclinedByUser,
                    default,
                    summary);
            }

            confirmed = true;
            var result = await control.RequestAsync("confirm", new JsonObject { ["token"] = token }, onApprovalWaiting, cancellationToken)
                .ConfigureAwait(false);
            return new ConfirmationOutcome(ConfirmationStatus.Done, result, summary);
        }
        catch (TawkControlException ex) when (ex.Message.Contains(token, StringComparison.Ordinal))
        {
            throw new TawkControlException(ex.Error with { Message = ex.Message.Replace(token, "[token]", StringComparison.Ordinal) });
        }
        finally
        {
            if (!confirmed)
            {
                await CancelQuietlyAsync(token).ConfigureAwait(false);
            }
        }
    }

    private static bool NeedsConfirmation(JsonElement result, out string token, out string summary)
    {
        token = string.Empty;
        summary = string.Empty;
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("needs_confirmation", out var needs)
            || needs.ValueKind != JsonValueKind.True
            || !result.TryGetProperty("token", out var tokenElement)
            || tokenElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        token = tokenElement.GetString() ?? string.Empty;
        summary = result.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString() ?? string.Empty
            : "tawk asks you to confirm this change.";
        return token.Length > 0;
    }

    private async Task CancelQuietlyAsync(string token)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await control.RequestAsync("cancel_confirmation", new JsonObject { ["token"] = token }, null, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TawkControlException or OperationCanceledException)
        {
            // The token expires by itself within 5 minutes and works only on this connection.
        }
    }
}
