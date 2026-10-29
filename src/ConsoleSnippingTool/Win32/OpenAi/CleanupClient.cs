using System.Text;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32.OpenAi;

/// The Cleanup pass: one POST to v1/responses with the core's instructions and the Transcript (spec: Cleanup).
internal static class CleanupClient
{
    /// Returns the cleaned Transcript, "" when the model produced no text (a refusal, say). Throws the way
    /// ITranscriptionTransport.TranscribeAsync does, with the failure reason as the message.
    public static async Task<string> CleanAsync(string transcript, CleanupRequest request, CancellationToken cancellationToken)
    {
        var body = OpenAiJson.CleanupBody(request.Model, request.Instructions, transcript);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await OpenAiHttp
            .SendAsync(HttpMethod.Post, "v1/responses", request.ApiKey, content, cancellationToken)
            .ConfigureAwait(false);
        return OpenAiJson.ParseCleanupOutput(response);
    }
}
