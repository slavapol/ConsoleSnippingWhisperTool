using System.Text.Json;

namespace ConsoleSnippingTool.Win32.OpenAi;

/// The pure part of the transport to the Service: the request bodies the adapter sends and the parsing of what comes
/// back, with no HTTP in it so the tests need no network. Field names checked against the OpenAI docs on 2026-10-29
/// (speech-to-text guide; Responses API create and object references).
public static class OpenAiJson
{
    /// The Transcript in a transcription response: its "text" field. Throws FormatException, whose message is the
    /// failure reason, when the field is not there.
    public static string ParseTranscript(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("text", out var text)
            || text.ValueKind != JsonValueKind.String)
        {
            throw new FormatException("response has no \"text\" field");
        }

        return text.GetString()!;
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
    public static string ParseCleanupOutput(string json)
    {
        using var document = JsonDocument.Parse(json);
        var text = new System.Text.StringBuilder();
        foreach (var item in document.RootElement.GetProperty("output").EnumerateArray())
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

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }
}
