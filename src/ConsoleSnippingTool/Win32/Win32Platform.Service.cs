using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    private const string TransportNotImplemented = "transport not implemented";

    // The HTTP transport to the Service is ticket 06. Until then the warm-up does nothing and every request fails
    // with the reason above, handed to the core through the message window the way the real transport will hand
    // results from the thread pool: after this call has returned, on the message loop.

    public void WarmUpService(string apiKey)
    {
    }

    public void BeginTranscription(int sequence, AudioClip clip, TranscriptionRequest request) =>
        _window.Post(() => AttachedCore.OnTranscriptionFailed(sequence, TransportNotImplemented));

    public void BeginCleanup(int sequence, string transcript, CleanupRequest request) =>
        _window.Post(() => AttachedCore.OnCleanupFailed(sequence, TransportNotImplemented));
}
