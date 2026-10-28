using System.Globalization;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// The one test seam: a recording IPlatform. One fake per test; there is no Reset.
//
// Every call appends exactly one string to Calls: the member name, then its arguments in parentheses,
// comma-separated with no spaces. Free-text arguments are recorded verbatim. One example per member group:
//
//   Single instance  TryAcquireInstance()  SignalPreviousInstance()  WaitForPreviousInstanceExit(5s)  TerminatePreviousInstance()
//   Config           ReadConfigText()  WriteConfigText(<json verbatim>)
//   Hotkeys          RegisterHotkey(Ctrl+Win+Z)  UnregisterHotkey()  RegisterCancelKey()  UnregisterCancelKey()
//   Capture          InstallMouseHook()  RemoveMouseHook()  StartArmedTimer(30s)  StopArmedTimer()  TryCaptureToClipboard(5,10,6x11)
//   Sounds           TrySetSound(Ready,BuiltIn)  TrySetSound(Done,None)  TrySetSound(Ready,File(click.wav))  PlaySound(Done)
//   Console          ShowConsole()  HideConsole()  WriteConsoleLine(<line verbatim>)
//   Tray             SetTrayTooltip(ConsoleSnippingTool (Ctrl+Win+Z))  SetTrayMenuState(showEnabled:true,hideEnabled:false)
//                    ShowBalloon(<title>: <text>)  RemoveTrayIcon()
//   Process          Exit()
//
// Formats: a TimeSpan is whole or fractional seconds followed by "s" (30s, 0.5s); a Hotkey is its Text; a Region is
// Left,Top,WidthxHeight; a SoundSource is BuiltIn, None or File(path); a bool is lowercase true/false.
public sealed class FakePlatform : IPlatform
{
    public List<string> Calls { get; } = [];

    // Knobs. Each has the "happy path" default so a test only sets the one it is about.

    /// Returned by ReadConfigText; null = no config file.
    public string? ConfigText { get; set; }

    /// Dequeued per TryAcquireInstance call; an empty queue means true.
    public Queue<bool> AcquireResults { get; set; } = new();

    public bool WaitForPreviousResult { get; set; } = true;

    public bool RegisterHotkeyResult { get; set; } = true;

    public bool CaptureResult { get; set; } = true;

    /// Per-signal result of TrySetSound; a missing signal means true.
    public Dictionary<Signal, bool> SoundLoadResults { get; set; } = new();

    /// The json of the last WriteConfigText call, for assertions that do not want to match the whole Calls entry.
    public string? WrittenConfigText { get; private set; }

    // Single instance

    public bool TryAcquireInstance()
    {
        Record("TryAcquireInstance()");
        return AcquireResults.Count == 0 || AcquireResults.Dequeue();
    }

    public void SignalPreviousInstance() => Record("SignalPreviousInstance()");

    public bool WaitForPreviousInstanceExit(TimeSpan timeout)
    {
        Record($"WaitForPreviousInstanceExit({Seconds(timeout)})");
        return WaitForPreviousResult;
    }

    public void TerminatePreviousInstance() => Record("TerminatePreviousInstance()");

    // Config file beside the exe

    public string? ReadConfigText()
    {
        Record("ReadConfigText()");
        return ConfigText;
    }

    public void WriteConfigText(string json)
    {
        Record($"WriteConfigText({json})");
        WrittenConfigText = json;
    }

    // Hotkeys

    public bool RegisterHotkey(Hotkey hotkey)
    {
        Record($"RegisterHotkey({hotkey.Text})");
        return RegisterHotkeyResult;
    }

    public void UnregisterHotkey() => Record("UnregisterHotkey()");

    public void RegisterCancelKey() => Record("RegisterCancelKey()");

    public void UnregisterCancelKey() => Record("UnregisterCancelKey()");

    // Capture

    public void InstallMouseHook() => Record("InstallMouseHook()");

    public void RemoveMouseHook() => Record("RemoveMouseHook()");

    public void StartArmedTimer(TimeSpan duration) => Record($"StartArmedTimer({Seconds(duration)})");

    public void StopArmedTimer() => Record("StopArmedTimer()");

    public bool TryCaptureToClipboard(Region region)
    {
        Record($"TryCaptureToClipboard({region.Left},{region.Top},{region.Width}x{region.Height})");
        return CaptureResult;
    }

    // Sounds

    public bool TrySetSound(Signal signal, SoundSource source)
    {
        Record($"TrySetSound({signal},{Describe(source)})");
        return !SoundLoadResults.TryGetValue(signal, out var loaded) || loaded;
    }

    public void PlaySound(Signal signal) => Record($"PlaySound({signal})");

    // Console

    public void ShowConsole() => Record("ShowConsole()");

    public void HideConsole() => Record("HideConsole()");

    public void WriteConsoleLine(string line) => Record($"WriteConsoleLine({line})");

    // Tray

    public void SetTrayTooltip(string text) => Record($"SetTrayTooltip({text})");

    public void SetTrayMenuState(bool showEnabled, bool hideEnabled) =>
        Record($"SetTrayMenuState(showEnabled:{Bool(showEnabled)},hideEnabled:{Bool(hideEnabled)})");

    public void ShowBalloon(string title, string text) => Record($"ShowBalloon({title}: {text})");

    public void RemoveTrayIcon() => Record("RemoveTrayIcon()");

    // Process

    public void Exit() => Record("Exit()");

    private void Record(string call) => Calls.Add(call);

    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString(CultureInfo.InvariantCulture) + "s";

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Describe(SoundSource source) => source switch
    {
        SoundSource.BuiltIn => "BuiltIn",
        SoundSource.None => "None",
        SoundSource.File file => $"File({file.Path})",
        _ => source.ToString() ?? "?",
    };
}
