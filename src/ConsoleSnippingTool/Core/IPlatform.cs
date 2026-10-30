namespace ConsoleSnippingTool.Core;

public interface IPlatform
{
    // Single instance
    bool TryAcquireInstance();
    void SignalPreviousInstance();
    bool WaitForPreviousInstanceExit(TimeSpan timeout);
    void TerminatePreviousInstance();

    // Config file beside the exe
    string? ReadConfigText();              // null when the file does not exist
    void WriteConfigText(string json);

    // Hotkeys
    bool RegisterHotkey(Hotkey hotkey);    // false when another app owns it
    void UnregisterHotkey();
    void RegisterCancelKey();              // Esc, only while Armed (while Listening on the Microphone the keyboard hook takes Esc)
    void UnregisterCancelKey();
    bool RegisterMicrophoneHotkey(Hotkey hotkey); // the Microphone Hotkey; false when another app owns it
    void UnregisterMicrophoneHotkey();
    bool RegisterPlaybackHotkey(Hotkey hotkey);  // posts OnPlaybackHotkey on each press, never on autorepeat; false when another app owns it
    void UnregisterPlaybackHotkey();

    // Timer (renamed from StartArmedTimer/StopArmedTimer: one timer now serves Armed and Listening)
    void StartTimer(TimeSpan duration);   // restarts if already running
    void StopTimer();

    // Capture
    void InstallMouseHook();
    void RemoveMouseHook();
    bool TryCaptureToClipboard(Region region); // copies the screen region and sets PNG + DIB; false on clipboard failure after retries

    // Dictation
    void InstallKeyboardHook(Hotkey chord);   // posts OnMicrophoneKeyUp once when any key of the chord goes up, and OnCancelKey for Esc; swallows only Esc
    void RemoveKeyboardHook();
    bool TryOpenSource(DictationSource source, out string deviceName); // the Source's default communication endpoint; false when it cannot be opened; deviceName is its friendly name, empty when unknown
    AudioClip CloseSource();                  // stops capturing and returns everything recorded since TryOpenSource
    void WarmUpService(string apiKey);        // fire-and-forget connection warm-up; result ignored
    void BeginTranscription(int sequence, AudioClip clip, TranscriptionRequest request); // later posts OnTranscript or OnTranscriptionFailed with the same sequence
    void BeginCleanup(int sequence, string transcript, CleanupRequest request);          // later posts OnCleanedTranscript or OnCleanupFailed
    bool TryCopyText(string text);                        // Copy: a plain Unicode-text clipboard write with the shared retry; false when the clipboard stayed locked
    bool TryInsertText(string text, out string reason);   // Insert: paste with restore (ADR-0003); reason is set when false
    string? ReadEnvironmentVariable(string name);

    // Sounds
    bool TrySetSound(Signal signal, SoundSource source); // false when a File could not be loaded
    void PlaySound(Signal signal);                       // asynchronous

    // Console
    void ShowConsole();
    void HideConsole();
    void WriteConsoleLine(string line);

    // Tray
    void SetTrayTooltip(string text);
    void SetTrayMenuState(bool showEnabled, bool hideEnabled);
    void ShowBalloon(string title, string text);
    void RemoveTrayIcon();

    // Process
    void Exit();                           // stops the message loop
}
