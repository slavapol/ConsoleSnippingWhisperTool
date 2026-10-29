namespace ConsoleSnippingTool.Core;

public sealed partial class SnipperCore
{
    // The Dictation state machine is ticket 03; until then every Dictation event is ignored.

    /// The Dictation Hotkey went down (a held chord may repeat it).
    public void OnDictationHotkey()
    {
    }

    /// Any key of the held chord went up: the Release.
    public void OnDictationKeyUp()
    {
    }

    /// The Service returned the Transcript of the Dictation with this sequence number.
    public void OnTranscript(int sequence, string text)
    {
    }

    public void OnTranscriptionFailed(int sequence, string reason)
    {
    }

    /// The Cleanup pass returned its rewrite of the Transcript with this sequence number.
    public void OnCleanedTranscript(int sequence, string text)
    {
    }

    public void OnCleanupFailed(int sequence, string reason)
    {
    }
}
