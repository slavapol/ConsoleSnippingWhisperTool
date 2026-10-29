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
//                    RegisterDictationHotkey(Ctrl+Win+X)  UnregisterDictationHotkey()
//   Timer            StartTimer(30s)  StopTimer()
//   Capture          InstallMouseHook()  RemoveMouseHook()  TryCaptureToClipboard(5,10,6x11)
//   Dictation        InstallKeyboardHook(Ctrl+Win+X)  RemoveKeyboardHook()  TryOpenMicrophone()  CloseMicrophone()  WarmUpService()
//                    BeginTranscription(seq:1,clip:3s,model:gpt-transcribe,language:null,prompt:null)
//                    BeginCleanup(seq:1,model:gpt-6-luna,transcript:<text verbatim>)  TryInsertText(<text verbatim>)
//                    ReadEnvironmentVariable(OPENAI_API_KEY)
//   Sounds           TrySetSound(Ready,BuiltIn)  TrySetSound(Done,None)  TrySetSound(Ready,File(click.wav))  PlaySound(Done)
//   Console          ShowConsole()  HideConsole()  WriteConsoleLine(<line verbatim>)
//   Tray             SetTrayTooltip(ConsoleSnippingTool (Ctrl+Win+Z))  SetTrayMenuState(showEnabled:true,hideEnabled:false)
//                    ShowBalloon(<title>: <text>)  RemoveTrayIcon()
//   Process          Exit()
//
// Formats: a TimeSpan is whole or fractional seconds followed by "s" (30s, 0.5s); a Hotkey is its Text; a Region is
// Left,Top,WidthxHeight; an AudioClip is its Duration as a TimeSpan (3s); a SoundSource is its ToString (BuiltIn, None
// or File(path)); a bool is lowercase true/false; a null string is the word null. An API key is never recorded:
// WarmUpService, BeginTranscription and BeginCleanup leave it out of Calls, and LastTranscriptionRequest and
// LastCleanupRequest hold the full requests for a test that asserts the key or the Cleanup instructions.
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

    public bool RegisterDictationHotkeyResult { get; set; } = true;

    public bool CaptureResult { get; set; } = true;

    /// Per-signal result of TrySetSound; a missing signal means true.
    public Dictionary<Signal, bool> SoundLoadResults { get; set; } = new();

    /// When true, WriteConfigText records the call and then throws, as the real file write does on a read-only exe folder.
    public bool WriteConfigThrows { get; set; }

    /// The json of the last WriteConfigText call, for assertions that do not want to match the whole Calls entry.
    public string? WrittenConfigText { get; private set; }

    /// Returned by TryOpenMicrophone.
    public bool MicrophoneOpens { get; set; } = true;

    /// Returned by CloseMicrophone: 3 s of silence at 16 kHz.
    public AudioClip MicrophoneClip { get; set; } = new(new byte[3 * 16000 * 2], 16000);

    /// Returned by TryInsertText.
    public bool InsertResult { get; set; } = true;

    /// The reason TryInsertText gives when InsertResult is false.
    public string InsertFailureReason { get; set; } = "clipboard locked";

    /// What ReadEnvironmentVariable can see; a name that is not here reads as null.
    public Dictionary<string, string?> Environment { get; set; } = new();

    /// The request of the last BeginTranscription, API key included; null until the first call.
    public TranscriptionRequest? LastTranscriptionRequest { get; private set; }

    /// The request of the last BeginCleanup, API key and instructions included; null until the first call.
    public CleanupRequest? LastCleanupRequest { get; private set; }

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
        if (WriteConfigThrows)
        {
            throw new UnauthorizedAccessException("Access to the path 'config.json' is denied.");
        }

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

    public bool RegisterDictationHotkey(Hotkey hotkey)
    {
        Record($"RegisterDictationHotkey({hotkey.Text})");
        return RegisterDictationHotkeyResult;
    }

    public void UnregisterDictationHotkey() => Record("UnregisterDictationHotkey()");

    // Timer

    public void StartTimer(TimeSpan duration) => Record($"StartTimer({Seconds(duration)})");

    public void StopTimer() => Record("StopTimer()");

    // Capture

    public void InstallMouseHook() => Record("InstallMouseHook()");

    public void RemoveMouseHook() => Record("RemoveMouseHook()");

    public bool TryCaptureToClipboard(Region region)
    {
        Record($"TryCaptureToClipboard({region.Left},{region.Top},{region.Width}x{region.Height})");
        return CaptureResult;
    }

    // Dictation

    public void InstallKeyboardHook(Hotkey chord) => Record($"InstallKeyboardHook({chord.Text})");

    public void RemoveKeyboardHook() => Record("RemoveKeyboardHook()");

    public bool TryOpenMicrophone()
    {
        Record("TryOpenMicrophone()");
        return MicrophoneOpens;
    }

    public AudioClip CloseMicrophone()
    {
        Record("CloseMicrophone()");
        return MicrophoneClip;
    }

    public void WarmUpService(string apiKey) => Record("WarmUpService()");

    public void BeginTranscription(int sequence, AudioClip clip, TranscriptionRequest request)
    {
        Record($"BeginTranscription(seq:{sequence},clip:{Seconds(clip.Duration)},model:{request.Model},language:{Text(request.Language)},prompt:{Text(request.Prompt)})");
        LastTranscriptionRequest = request;
    }

    public void BeginCleanup(int sequence, string transcript, CleanupRequest request)
    {
        Record($"BeginCleanup(seq:{sequence},model:{request.Model},transcript:{transcript})");
        LastCleanupRequest = request;
    }

    public bool TryInsertText(string text, out string reason)
    {
        Record($"TryInsertText({text})");
        reason = InsertResult ? "" : InsertFailureReason;
        return InsertResult;
    }

    public string? ReadEnvironmentVariable(string name)
    {
        Record($"ReadEnvironmentVariable({name})");
        return Environment.GetValueOrDefault(name);
    }

    // Sounds

    public bool TrySetSound(Signal signal, SoundSource source)
    {
        Record($"TrySetSound({signal},{source})");
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

    private static string Text(string? value) => value ?? "null";
}
