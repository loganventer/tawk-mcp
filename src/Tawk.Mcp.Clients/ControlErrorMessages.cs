using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients;

/// <summary>Turns tawk's error codes into plain guidance for the model and the user.</summary>
public static partial class ControlErrorMessages
{
    public static string Describe(TawkControlException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var error = exception.Error;
        return exception.Code switch
        {
            ControlErrorCode.Declined => "The user declined this in tawk, so nothing was done. Do not retry unless the user asks.",
            ControlErrorCode.TimedOut => "Nobody approved this in tawk within 2 minutes, so nothing was done. Ask the user to watch tawk if they still want it.",
            ControlErrorCode.NotAllowed => "tawk does not allow this. Sending needs access = send and managing tawk needs access = manage under [automation] in tawk's settings, "
                + "the chat must be in the allowed chats list, and some settings can never be changed from outside. tawk said: " + error.Message,
            ControlErrorCode.Ambiguous => Ambiguous(error),
            ControlErrorCode.RateLimited => string.Create(
                CultureInfo.InvariantCulture,
                $"tawk's write limit (writes_per_minute) was reached. Try again in {error.RetryAfter ?? 60} seconds."),
            ControlErrorCode.Offline => "tawk cannot do this right now: " + error.Message,
            ControlErrorCode.NotFound => "Nothing visible matches. Locked and hidden chats are never shown. tawk said: " + error.Message,
            ControlErrorCode.DraftExists => "That chat already has a draft in tawk, so it was left alone. Ask the user to send or clear it first.",
            ControlErrorCode.NotRunning => error.Message,
            ControlErrorCode.BadToken => "The confirmation expired or was already used, so nothing was done. Ask the user whether to start again.",
            ControlErrorCode.Unsupported => "The backend tawk uses cannot do this. tawk said: " + error.Message,
            ControlErrorCode.Failed => "tawk tried and could not do it: " + error.Message,
            _ => "tawk rejected the request: " + error.Message,
        };
    }

    private static string Ambiguous(ControlError error)
    {
        var text = new StringBuilder("More than one chat matches that name. Ask the user which one they mean, then call again with its jid.");
        foreach (var candidate in error.Candidates ?? [])
        {
            text.Append("\n- ").Append(Plain(candidate.Name)).Append(" <").Append(Plain(candidate.Jid)).Append('>');
        }

        return text.ToString();
    }

    // Chat names are chosen by other people, so keep them to one short plain line.
    private static string Plain(string value)
    {
        var flat = Unsafe().Replace(value.ReplaceLineEndings(" "), string.Empty).Trim();
        return flat.Length <= 80 ? flat : flat[..77] + "...";
    }

    [GeneratedRegex("[<>`]+")]
    private static partial Regex Unsafe();
}
