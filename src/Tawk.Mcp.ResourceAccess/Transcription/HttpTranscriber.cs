using System.Net.Http.Headers;
using System.Text.Json;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Sends the file to a transcriber the user runs, with the OpenAI audio API that whisper.cpp's server,
/// faster-whisper-server and others speak. The address comes only from the user's own settings.
/// </summary>
public sealed class HttpTranscriber(HttpClient client, TranscriptionOptions options) : ITranscriber
{
    public string Name => "http";

    public async Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = options.Url ?? throw new TranscriptionException("no transcriber is set");
        var model = request.Model;
        var endpoint = new Uri(
            address.AbsoluteUri.TrimEnd('/') + "/v1/audio/" + (request.Task == TranscriptionTask.Translate ? "translations" : "transcriptions"));

        try
        {
            var file = new FileStream(request.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            await using (file.ConfigureAwait(false))
            {
                using var form = new MultipartFormDataContent();
                using var audio = new StreamContent(file);
                audio.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(audio, "file", Path.GetFileName(request.Path));
                using var modelPart = new StringContent(model);
                form.Add(modelPart, "model");
                using var format = new StringContent("verbose_json");
                form.Add(format, "response_format");
                using var language = new StringContent(request.LanguageHint ?? string.Empty);
                if (request.LanguageHint is not null)
                {
                    form.Add(language, "language");
                }

                using var prompt = new StringContent(request.Prompt ?? string.Empty);
                if (request.Prompt is not null)
                {
                    form.Add(prompt, "prompt");
                }

                using var response = await client.PostAsync(endpoint, form, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new TranscriptionException($"the transcriber answered {(int)response.StatusCode}");
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return Read(body, model);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new TranscriptionException("the transcriber could not be reached", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TranscriptionException("the audio file could not be read", ex);
        }
    }

    // Some servers answer plain text whatever format was asked for.
    private static Transcript Read(string body, string model)
    {
        var trimmed = body.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return new Transcript(trimmed, null, model, null);
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            var text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : string.Empty;
            var heard = root.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
            double? duration = root.TryGetProperty("duration", out var d) && d.TryGetDouble(out var seconds) ? seconds : null;
            return new Transcript(text.Trim(), heard, model, duration);
        }
        catch (JsonException ex)
        {
            throw new TranscriptionException("the transcriber's answer could not be read", ex);
        }
    }
}
