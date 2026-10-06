using System.Globalization;
using Microsoft.Extensions.Logging;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Transcription;

namespace Tawk.Mcp.Clients;

/// <summary>
/// Offers every voice note another person sends for automatic transcription. Whether it happens is the
/// user's choice, read as each one arrives, so the switch in tawk's settings takes hold at once. The
/// transcript follows the message as its own event.
/// </summary>
public sealed partial class AutoTranscriptionSink(ITranscriptionManager transcription, ILogger<AutoTranscriptionSink> logger) : IEventSink
{
    public async Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (update.Event is MessageEvent { Message: { Type: "audio", FromMe: false, Deleted: false } message } incoming)
        {
            try
            {
                await transcription
                    .StartAutomaticAsync(message.Id, incoming.Account?.Id.ToString(CultureInfo.InvariantCulture), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TranscriptionException ex)
            {
                LogNotStarted(ex.Message);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A voice note was not transcribed automatically: {Reason}")]
    private partial void LogNotStarted(string reason);
}
