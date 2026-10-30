using ConsoleSnippingTool.Core;
using ConsoleSnippingTool.Win32.OpenAi;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// The warm-up's own limit. The two requests run under the Timeout the core put on each, through a
    /// CancellationTokenSource per call; the HttpClient itself has none (spec: Requests and their timeout).
    private static readonly TimeSpan WarmUpTimeout = TimeSpan.FromSeconds(20);

    /// The upload transport (ADR-0004); a streaming one would be chosen here from config.
    private readonly ITranscriptionTransport _transport = new UploadTransport();

    // Every call to the Service runs on the thread pool and hands its outcome to the core through the message window,
    // so the core stays on the message loop. The request records hold the key and their ToString() leaves it out:
    // nothing here logs or interpolates one, and the key goes only into the bearer header, per request.

    /// GET v1/models with the bearer header so the pooled TLS connection exists before the Release. Whatever comes
    /// back, including a failure, is dropped: the request that follows reports its own.
    public void WarmUpService(string apiKey) =>
        _ = Task.Run(async () =>
        {
            try
            {
                using var timeout = new CancellationTokenSource(WarmUpTimeout);
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
            request.Timeout,
            token => _transport.TranscribeAsync(clip, request, token),
            transcript => AttachedCore.OnTranscript(sequence, transcript.Text, transcript.Languages),
            reason => AttachedCore.OnTranscriptionFailed(sequence, reason));

    public void BeginCleanup(int sequence, string transcript, CleanupRequest request) =>
        Begin(
            request.ApiKey,
            request.Timeout,
            token => CleanupClient.CleanAsync(transcript, request, token),
            text => AttachedCore.OnCleanedTranscript(sequence, text),
            reason => AttachedCore.OnCleanupFailed(sequence, reason));

    /// Runs the call on the thread pool under the request's timeout and posts exactly one of the two outcomes, the
    /// call's result or the failure reason, to the message loop, after this method has returned.
    private void Begin<TResult>(
        string apiKey,
        TimeSpan requestTimeout,
        Func<CancellationToken, Task<TResult>> call,
        Action<TResult> succeeded,
        Action<string> failed) =>
        _ = Task.Run(async () =>
        {
            TResult result;
            try
            {
                using var timeout = new CancellationTokenSource(requestTimeout);
                result = await call(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                var reason = FailureReason(e, apiKey, requestTimeout);
                _window.Post(() => failed(reason));
                return;
            }

            _window.Post(() => succeeded(result));
        });

    /// The failure reason for a call that threw: the timeout that applied for a cancellation, otherwise the
    /// exception's own message (a ServiceException carries the status and the Service's message, an
    /// HttpRequestException the network's, a FormatException the shape problem). No known message quotes the bearer
    /// header, and should one ever do so the key is blanked before the reason leaves the adapter.
    private static string FailureReason(Exception e, string apiKey, TimeSpan requestTimeout)
    {
        if (e is OperationCanceledException)
        {
            return $"timed out after {requestTimeout.TotalSeconds:0} s";
        }

        return apiKey.Length == 0 ? e.Message : e.Message.Replace(apiKey, "<key>");
    }
}
