using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Media;

namespace Tawk.Mcp.ResourceAccess.Media;

public sealed class TawkMediaSource(ITawkControl control) : ITawkMediaSource
{
    public async Task<MediaFile?> LocateAsync(string messageId, TimeSpan wait, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(wait);

        // Listening starts before the request, so a download that ends at once is not missed.
        var events = control.Events.GetAsyncEnumerator(limit.Token);
        var next = events.MoveNextAsync().AsTask();
        try
        {
            var answer = await AskAsync(messageId, limit.Token, cancellationToken).ConfigureAwait(false);
            if (Text(answer, "path") is { Length: > 0 } path)
            {
                return new MediaFile(messageId, path, Text(answer, "type"), Chat(answer)) { Transcribe = ControlLineCodec.MayTranscribe(answer) };
            }

            while (await next.ConfigureAwait(false))
            {
                if (events.Current is MediaReadyEvent ready && string.Equals(ready.MessageId, messageId, StringComparison.Ordinal))
                {
                    return new MediaFile(messageId, ready.Path, ready.Type, ready.Chat) { Transcribe = ready.Transcribe };
                }

                next = events.MoveNextAsync().AsTask();
            }

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            // An enumerator may not be disposed while a read is under way.
            await limit.CancelAsync().ConfigureAwait(false);
            try
            {
                await next.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The read was let go.
            }

            await events.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<JsonElement> AskAsync(string messageId, CancellationToken limit, CancellationToken cancellationToken)
    {
        try
        {
            return await control.RequestAsync("download_media", new JsonObject { ["message_id"] = messageId }, limit).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MediaException("tawk did not answer in time. If it is asking you to approve the download, answer it and try again.");
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static ChatRef? Chat(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty("chat", out var chat) && chat.ValueKind == JsonValueKind.Object
            ? ControlLineCodec.Deserialize<ChatRef>(chat)
            : null;
}
