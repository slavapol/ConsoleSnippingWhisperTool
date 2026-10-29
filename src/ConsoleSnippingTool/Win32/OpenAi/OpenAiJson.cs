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

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }
}
