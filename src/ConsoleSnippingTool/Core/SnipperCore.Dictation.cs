using System.Globalization;

namespace ConsoleSnippingTool.Core;

public sealed partial class SnipperCore
{
    private TimeSpan ListeningTimeout => TimeSpan.FromSeconds(_config.ListeningTimeoutSeconds);

    /// The Dictation Hotkey went down (a held chord may repeat it).
    public void OnDictationHotkey()
    {
        StartListening();
    }

    /// Opens the microphone and starts reading the user's speech; Ready is the one signal that it worked.
    private void StartListening()
    {
        if (_apiKey is not { } apiKey)
        {
            Report(NoApiKeyMessage);
            return;
        }

        if (!_platform.TryOpenMicrophone())
        {
            Report("No microphone found");
            return;
        }

        _platform.StartTimer(ListeningTimeout);
        _platform.RegisterCancelKey();
        _platform.InstallKeyboardHook(_config.DictationHotkey);
        _platform.WarmUpService(apiKey);
        _platform.PlaySound(Signal.Ready);
        _listeningSince = _clock();
        _state = State.Listening;
        Log("Listening");
    }

    /// Any key of the held chord went up: the Release.
    public void OnDictationKeyUp()
    {
        if (_state == State.Listening)
        {
            Release();
        }
    }

    /// Ends Listening and sends what was heard; Sent is the signal that the audio is on its way.
    private void Release()
    {
        var clip = StopListening();
        _dictationSequence++;
        _sentAt = _clock();
        _platform.PlaySound(Signal.Sent);
        _platform.BeginTranscription(_dictationSequence, clip, TranscriptionRequest());
        _state = State.Transcribing;
        Log($"Sent {Seconds(clip.Duration)} s of audio");
    }

    /// The key is the one Listening was entered with, so it is never missing here.
    private TranscriptionRequest TranscriptionRequest() =>
        new(_apiKey ?? throw new InvalidOperationException("Dictation without an API key"), _config.TranscriptionModel, _config.Language, _config.Prompt);

    /// Stops reading the user's speech: hook off, Esc released, cap stopped, microphone closed. The tail of every
    /// Release and Discard; the clip is what the microphone heard.
    private AudioClip StopListening()
    {
        _platform.RemoveKeyboardHook();
        _platform.UnregisterCancelKey();
        _platform.StopTimer();
        var clip = _platform.CloseMicrophone();
        _state = State.Idle;
        return clip;
    }

    /// Seconds to one decimal, the way the Console reports audio length and wait (3.0, 4.2), whatever the user's culture.
    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);

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
