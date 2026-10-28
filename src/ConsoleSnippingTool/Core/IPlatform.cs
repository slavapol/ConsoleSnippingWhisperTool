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
    void RegisterCancelKey();              // Esc, only while Armed
    void UnregisterCancelKey();

    // Capture
    void InstallMouseHook();
    void RemoveMouseHook();
    void StartArmedTimer(TimeSpan duration);   // restarts if already running
    void StopArmedTimer();
    bool TryCaptureToClipboard(Region region); // copies the screen region and sets PNG + DIB; false on clipboard failure after retries

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
