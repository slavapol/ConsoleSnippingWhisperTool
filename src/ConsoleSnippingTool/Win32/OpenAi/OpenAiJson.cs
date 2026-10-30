using System.Net.Http.Headers;
using System.Text.Json;

namespace ConsoleSnippingTool.Win32.OpenAi;

/// The pure part of the transport to the Service: the request bodies the adapter sends and the parsing of what comes
/// back, with no HTTP in it so the tests need no network. Field names checked against the OpenAI docs on 2026-10-29
/// (speech-to-text guide; Responses API create and object references).
public static class OpenAiJson
{
    /// The multipart form of the transcription request: the WAV as the file "dictation.wav", the model, response_format
    /// json so the Transcript comes back as the "text" field, one languages[] part per expected language (the
    /// gpt-transcribe parameter; the singular "language" belongs to the models being shut down), none for an empty
    /// list, and the prompt. Takes the fields, not the TranscriptionRequest, so the key cannot end up in a form.
    public static MultipartFormDataContent TranscriptionForm(byte[] wav, string model, IReadOnlyList<string> languages, string? prompt)
    {
        var file = new ByteArrayContent(wav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        var form = new MultipartFormDataContent
        {
            Part(file, "file", "dictation.wav"),
            Part(new StringContent(model), "model"),
            Part(new StringContent("json"), "response_format"),
        };
        foreach (var code in languages)
        {
            form.Add(Part(new StringContent(code), "languages[]"));
        }

        if (prompt is not null)
        {
            form.Add(Part(new StringContent(prompt), "prompt"));
        }

        return form;
    }

    // A form part with its Content-Disposition written as curl writes it: name and filename quoted, no filename*.
    // MultipartFormDataContent.Add(content, name) would leave token names unquoted and add filename*, and not every
    // multipart parser takes that.
    private static HttpContent Part(HttpContent content, string name, string? fileName = null)
    {
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = $"\"{name}\"",
            FileName = fileName is null ? null : $"\"{fileName}\"",
        };
        return content;
    }

    /// The Transcript and the languages the Service detected in a transcription response: the "text" field, and each
    /// entry under "languages", a code string or an object with a "code" (none when that field is absent or empty).
    /// Throws FormatException, whose message is the failure reason, when the body is not JSON or the text is not there.
    public static Transcript ParseTranscript(string json)
    {
        using var document = ParseResponse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("text", out var text)
            || text.ValueKind != JsonValueKind.String)
        {
            throw new FormatException("response has no \"text\" field");
        }

        return new Transcript(text.GetString()!, DetectedLanguages(document.RootElement));
    }

    // The code of every languages[] entry that has one: the entry itself when it is a string, its "code" when it is an
    // object. A languages field of another shape, or an entry of neither, is no detected language and never a failed
    // Dictation: the Transcript is what matters.
    private static IReadOnlyList<string> DetectedLanguages(JsonElement response)
    {
        if (!response.TryGetProperty("languages", out var languages) || languages.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var codes = new List<string>();
        foreach (var entry in languages.EnumerateArray())
        {
            var code = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("code", out var field) ? field : entry;
            if (code.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(code.GetString()))
            {
                codes.Add(code.GetString()!);
            }
        }

        return codes;
    }

    /// The failure reason for a 4xx or 5xx response: the status code and the first line of the body's error.message,
    /// or the status code and reason phrase when the body is not that shape (a gateway page, say). Never the key: the
    /// Service's own message redacts it to a prefix and asterisks, and nothing of the request is echoed here.
    public static string FailureReason(int statusCode, string? reasonPhrase, string body)
    {
        var detail = TryErrorMessage(body) ?? reasonPhrase;
        return string.IsNullOrWhiteSpace(detail) ? statusCode.ToString() : $"{statusCode} {FirstLine(detail)}";
    }

    // error.message of the { "error": { "message", "type", "param", "code" } } body OpenAI sends with an error status;
    // null for any other body.
    private static string? TryErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON at all; the reason phrase stands in.
        }

        return null;
    }

    /// The body of the Cleanup request to the Responses API: the model, the core's instructions, the Transcript as the
    /// input string, and reasoning switched off for speed (spec: Cleanup). Takes the fields, not the CleanupRequest,
    /// so the key cannot end up in a body.
    public static string CleanupBody(string model, string instructions, string transcript) =>
        JsonSerializer.Serialize(new
        {
            model,
            instructions,
            input = transcript,
            reasoning = new { effort = "none" },
        });

    /// The cleaned Transcript in a Responses object: the "text" of every output[].content[] part whose "type" is
    /// "output_text", concatenated. Reasoning items have no content and a refusal part has no text, so an output
    /// with nothing but those yields "". (Some SDKs add a top-level "output_text" for this; the raw JSON has none.)
    /// Throws FormatException, whose message is the failure reason, when the body is not JSON or the output array is
    /// not there.
    public static string ParseCleanupOutput(string json)
    {
        using var document = ParseResponse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("output", out var output)
            || output.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("response has no \"output\" field");
        }

        var text = new System.Text.StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) && type.ValueEquals("output_text")
                    && part.TryGetProperty("text", out var partText) && partText.ValueKind == JsonValueKind.String)
                {
                    text.Append(partText.GetString());
                }
            }
        }

        return text.ToString();
    }

    /// A success response body as a JSON document. A body that is not JSON (a gateway page behind a 200, say) is a
    /// FormatException whose message is the failure reason, as a missing field is; JsonException's own message would
    /// name a byte offset instead.
    private static JsonDocument ParseResponse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new FormatException("response is not JSON");
        }
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }
}
