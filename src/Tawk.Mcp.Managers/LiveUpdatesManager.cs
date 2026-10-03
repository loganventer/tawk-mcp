using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed partial class LiveUpdatesManager : ILiveUpdatesManager
{
    private readonly ITawkControl _control;
    private readonly IReadOnlyList<IEventSink> _sinks;
    private readonly ITranscriptFormatter _transcript;
    private readonly INotificationFormatter _notification;
    private readonly IUntrustedTextFence _fence;
    private readonly ILogger<LiveUpdatesManager> _logger;

    public LiveUpdatesManager(
        ITawkControl control,
        IEnumerable<IEventSink> sinks,
        ITranscriptFormatter transcript,
        INotificationFormatter notification,
        IUntrustedTextFence fence,
        ILogger<LiveUpdatesManager> logger)
    {
        _control = control;
        _sinks = [.. sinks];
        _transcript = transcript;
        _notification = notification;
        _fence = fence;
        _logger = logger;
    }

    public TawkConnectionState ConnectionState => _control.State;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var tawkEvent in _control.Events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (tawkEvent is ConnectionStateEvent { State: TawkConnectionState.Connected })
            {
                await SubscribeAllAsync(cancellationToken).ConfigureAwait(false);
            }

            var text = tawkEvent switch
            {
                MessageEvent message => Describe(message),
                ReadEvent read => _notification.Read(read.Chat, read.MessageId, read.Reader),
                MessageActivityEvent activity => Describe(activity),
                _ => null,
            };

            // The account goes first, outside the fenced text, so nothing in a message can pose as it.
            var update = new LiveUpdate(
                tawkEvent, text is not null && tawkEvent.Account is { } account ? _notification.Account(account) + "\n" + text : text);
            foreach (var sink in _sinks)
            {
                try
                {
                    await sink.OnUpdateAsync(update, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogSinkFailed(sink.GetType().Name, ex);
                }
            }
        }
    }

    private async Task SubscribeAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _control.RequestAsync("subscribe", new JsonObject { ["chats"] = "all" }, cancellationToken).ConfigureAwait(false);
        }
        catch (TawkControlException ex)
        {
            LogSubscribeFailed(ex.Message);
        }
    }

    private string Describe(MessageEvent message) =>
        _notification.Header(message.Chat, message.Message) + "\n"
        + _fence.Wrap("a new WhatsApp message", _transcript.FormatMessage(message.Message));

    // An edit carries the other person's new words, which are fenced like any message.
    private string Describe(MessageActivityEvent activity) =>
        activity is { Kind: ActivityKind.Edited, Message: { } edited }
            ? _notification.Activity(activity) + "\n" + _fence.Wrap("the edited WhatsApp message", _transcript.FormatMessage(edited))
            : _notification.Activity(activity);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not subscribe to tawk updates: {Reason}")]
    private partial void LogSubscribeFailed(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Sink} update sink failed")]
    private partial void LogSinkFailed(string sink, Exception exception);
}
