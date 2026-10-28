# 02 Platform seam, fake Platform, core skeleton with startup and single instance

Status: resolved
Blocked by: 01

Spec: `.scratch/console-snipping-tool/spec.md` → Core and Platform seam, Single instance, Tray, Testing Decisions. ADRs 0001 and 0002.

## Goal

The one test seam of the project exists and is frozen: the `IPlatform` interface below, a recording `FakePlatform` in the test project, and a `SnipperCore` whose `Start` already performs the single-instance handshake, reads config (defaults only for now; ticket 04 adds parsing), registers the Hotkey, and sets up the tray. All other core behaviour is stubbed so tickets 03, 04, 05 and 06 fill it in without touching each other's files.

## The seam (write it exactly like this; later tickets must not change it)

Namespace `ConsoleSnippingTool.Core`, file-per-type.

```csharp
public enum Signal { Ready, Corner, Done }

/// What a signal plays. BuiltIn = the synthesised click; None = silent; File = a .wav path.
public abstract record SoundSource
{
    public sealed record BuiltIn : SoundSource;
    public sealed record None : SoundSource;
    public sealed record File(string Path) : SoundSource;
}

[Flags] public enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 } // = MOD_* values

/// VirtualKey is the Win32 VK code. Text is the canonical display form, e.g. "Ctrl+Win+Z".
public readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey, string Text)
{
    public static Hotkey Default => new(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x5A, "Ctrl+Win+Z");
}

/// Inclusive rectangle in virtual-screen pixels.
public readonly record struct Region(int Left, int Top, int Width, int Height);

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
```

`SnipperCore` is a `partial class` split over these files so later tickets edit disjoint files:

- `SnipperCore.cs`: fields, constructor `SnipperCore(IPlatform platform)`, the `State` enum (`Idle`, `Armed`, `FirstCorner`), current state, current `Config` (for now a record with `Hotkey Hotkey = Hotkey.Default`, `int ArmedTimeoutSeconds = 30`, three `SoundSource` fields defaulting to `BuiltIn`; ticket 04 extends it with parsing), and a `Log(string line)` method that for now only calls `WriteConsoleLine` when the Console is visible (ticket 05 adds the ring).
- `SnipperCore.Startup.cs`: `Start()`, `OnReplaceRequested()`, `OnExit()`.
- `SnipperCore.Capture.cs`: `OnHotkey()`, `OnCancelKey()`, `bool OnLeftButtonDown(int x, int y)`, `bool OnRightButtonDown()`, `OnTimerElapsed()`: all stubs that do nothing and return `false`.
- `SnipperCore.Console.cs`: `OnShow()`, `OnHide()`: stubs.

Public events on the core are the only way the adapter talks to it; the core talks back only via `IPlatform`.

## Behaviour to implement in this ticket (through the seam, test-first)

`Start()`:

1. Single instance: `TryAcquireInstance()`; if false → `SignalPreviousInstance()`, `WaitForPreviousInstanceExit(5 s)`; if that is false → `TerminatePreviousInstance()`; then `TryAcquireInstance()` again (assume it succeeds). Log "Replaced previous instance".
2. Config: `ReadConfigText()`; when null, `WriteConfigText` with the default JSON (the exact shape in the spec's Configuration section, formatted with indentation) and use defaults. Parsing of non-null text is ticket 04: for now any non-null text also yields defaults.
3. Sounds: `TrySetSound` for each of the three signals with the config's source (all `BuiltIn` for now). A false return logs a warning and falls back to `TrySetSound(signal, BuiltIn)`.
4. Hotkey: `RegisterHotkey(config.Hotkey)`; on false, `ShowBalloon` and `Log` naming the Hotkey, keep running.
5. Tray: `SetTrayTooltip("ConsoleSnippingTool (Ctrl+Win+Z)")` using the Hotkey text; `SetTrayMenuState(showEnabled: true, hideEnabled: false)`.

`OnReplaceRequested()` → same as `OnExit()`.

`OnExit()`: if Armed or FirstCorner, cancel (call the capture file's cancel path; for this ticket a `private void Cancel(string reason)` in `SnipperCore.Capture.cs` that just sets Idle, ticket 03 completes it), then `UnregisterHotkey()`, `RemoveTrayIcon()`, `Exit()`.

`FakePlatform` (test project): implements `IPlatform`, appends one string per call to a public `Calls` list in a stable format such as `RegisterHotkey(Ctrl+Win+Z)`, `StartArmedTimer(30s)`, `TryCaptureToClipboard(5,10,6x11)`, `WriteConsoleLine(...)`. Knobs as public settable properties: `ConfigText` (string?), `AcquireResults` (queue of bools; default: true), `WaitForPreviousResult`, `RegisterHotkeyResult`, `CaptureResult`, `SoundLoadResults` (per signal). Provide a `Reset()`-free design: one fake per test.

Tests (xUnit v3, one file `StartupTests.cs`): fresh start writes default config and registers the default Hotkey and sets tooltip/menu; previous instance exits in time → signal, wait, acquire, no terminate; previous instance hangs → terminate; Hotkey taken → balloon, still sets tray; Exit while Idle → unregister, remove icon, exit, in that order.

## Acceptance

- `dotnet test` green. No Win32 code anywhere. The interface matches the listing above verbatim.

## Comments
- Resolved in 1fc91a9 (02: Platform seam, FakePlatform and SnipperCore skeleton with startup), fast-forwarded onto integration/console-snipping-tool; build and 9 tests green on integration. Notes for later tickets: OnExit already calls OnHide before UnregisterHotkey; Config has ToJson() with relaxed escaping so "+" stays literal; console visibility is the private ConsoleShown property in SnipperCore.cs; the sound-fallback log line from ticket 06 is already in the sound step.
