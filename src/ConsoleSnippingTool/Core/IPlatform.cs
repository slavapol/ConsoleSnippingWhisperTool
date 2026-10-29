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
    void RegisterCancelKey();              // Esc, only while Armed (while Listening the keyboard hook takes Esc)
    void UnregisterCancelKey();
    bool RegisterDictationHotkey(Hotkey hotkey); // false when another app owns it
    void UnregisterDictationHotkey();

    // Timer (renamed from StartArmedTimer/StopArmedTimer: one timer now serves Armed and Listening)
    void StartTimer(TimeSpan duration);   // restarts if already running
    void StopTimer();

    // Capture
    void InstallMouseHook();
    void RemoveMouseHook();
    bool TryCaptureToClipboard(Region region); // copies the screen region and sets PNG + DIB; false on clipboard failure after retries

    // Dictation
    void InstallKeyboardHook(Hotkey chord);   // posts OnDictationKeyUp once when any key of the chord goes up, and OnCancelKey for Esc; swallows only Esc
    void RemoveKeyboardHook();
    bool TryOpenMicrophone();                 // default communication capture device; false when there is none
    AudioClip CloseMicrophone();              // stops capturing and returns everything recorded since TryOpenMicrophone
    void WarmUpService(string apiKey);        // fire-and-forget connection warm-up; result ignored
    void BeginTranscription(int sequence, AudioClip clip, TranscriptionRequest request); // later posts OnTranscript or OnTranscriptionFailed with the same sequence
    void BeginCleanup(int sequence, string transcript, CleanupRequest request);          // later posts OnCleanedTranscript or OnCleanupFailed
    bool TryInsertText(string text, out string reason);   // paste with restore (ADR-0003); reason is set when false
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
