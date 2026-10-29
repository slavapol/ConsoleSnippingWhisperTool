using ConsoleSnippingTool.Core;
using ConsoleSnippingTool.Win32.OpenAi;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// Per request, through a CancellationTokenSource; the HttpClient itself has none (spec: Transcription request).
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    /// The upload transport (ADR-0004); a streaming one would be chosen here from config.
    private readonly ITranscriptionTransport _transport = new UploadTransport();

    // Every call to the Service runs on the thread pool and hands its outcome to the core through the message window,
    // so the core stays on the message loop. The request records hold the key and their ToString() prints it: nothing
    // here logs or interpolates one, and the key goes only into the bearer header, per request.

    /// GET v1/models with the bearer header so the pooled TLS connection exists before the Release. Whatever comes
    /// back, including a failure, is dropped: the request that follows reports its own.
    public void WarmUpService(string apiKey) =>
        _ = Task.Run(async () =>
        {
            try
            {
                using var timeout = new CancellationTokenSource(RequestTimeout);
                await OpenAiHttp.SendAsync(HttpMethod.Get, "v1/models", apiKey, content: null, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The warm-up is for its connection only.
            }
        });

    public void BeginTranscription(int sequence, AudioClip clip, TranscriptionRequest request) =>
        Begin(
            request.ApiKey,
            token => _transport.TranscribeAsync(clip, request, token),
            text => AttachedCore.OnTranscript(sequence, text),
            reason => AttachedCore.OnTranscriptionFailed(sequence, reason));

    public void BeginCleanup(int sequence, string transcript, CleanupRequest request) =>
        Begin(
            request.ApiKey,
            token => CleanupClient.CleanAsync(transcript, request, token),
            text => AttachedCore.OnCleanedTranscript(sequence, text),
            reason => AttachedCore.OnCleanupFailed(sequence, reason));

    /// Runs the call on the thread pool under the request timeout and posts exactly one of the two outcomes to the
    /// message loop, after this method has returned.
    private void Begin(
        string apiKey, Func<CancellationToken, Task<string>> call, Action<string> succeeded, Action<string> failed) =>
        _ = Task.Run(async () =>
        {
            string text;
            try
            {
                using var timeout = new CancellationTokenSource(RequestTimeout);
                text = await call(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                var reason = FailureReason(e, apiKey);
                _window.Post(() => failed(reason));
                return;
            }

            _window.Post(() => succeeded(text));
        });

    /// The failure reason for a call that threw: the timeout for a cancellation, otherwise the exception's own message
    /// (a ServiceException carries the status and the Service's message, an HttpRequestException the network's, a
    /// FormatException the shape problem). No known message quotes the bearer header, and should one ever do so the
    /// key is blanked before the reason leaves the adapter.
    private static string FailureReason(Exception e, string apiKey)
    {
        if (e is OperationCanceledException)
        {
            return $"timed out after {RequestTimeout.TotalSeconds:0} s";
        }

        return apiKey.Length == 0 ? e.Message : e.Message.Replace(apiKey, "<key>");
    }
}
