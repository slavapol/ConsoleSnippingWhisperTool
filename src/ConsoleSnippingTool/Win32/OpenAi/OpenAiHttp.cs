using System.Net.Http.Headers;

namespace ConsoleSnippingTool.Win32.OpenAi;

/// The one HttpClient of the process (spec: Transcription request) and the step every call to the Service shares: the
/// bearer header set per request from the key passed in, the body read as text, an error status turned into a
/// ServiceException whose message is the failure reason. The client has no timeout of its own; each caller brings a
/// token from its own CancellationTokenSource: the request's Timeout, or the warm-up's fixed 20 s.
internal static class OpenAiHttp
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler())
    {
        BaseAddress = new Uri("https://api.openai.com/"),
        Timeout = Timeout.InfiniteTimeSpan,
    };

    /// Sends and returns the response body, disposing the content. Throws ServiceException for a 4xx or 5xx,
    /// HttpRequestException when nothing answered, OperationCanceledException when the token fired.
    public static async Task<string> SendAsync(
        HttpMethod method, string path, string apiKey, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ServiceException(OpenAiJson.FailureReason((int)response.StatusCode, response.ReasonPhrase, body));
        }

        return body;
    }
}
