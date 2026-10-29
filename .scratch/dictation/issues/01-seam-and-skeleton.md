# 01 Dictation seam, fake Platform knobs, core and adapter skeleton

Status: ready-for-agent
Blocked by: none

Spec: `.scratch/dictation/spec.md` → Platform additions, Core states and transitions (the list of events only), Configuration (the record's new properties only). ADR-0003 and ADR-0004 for why the seam looks like this. Glossary: `GLOSSARY.md` section Dictating.

## Goal

The one test seam grows to cover Dictation and is then frozen: the `IPlatform` additions below, the new value types, the `Signal.Sent` member, the `Config` record's new properties with their defaults, a recording `FakePlatform` that knows every new member, a `SnipperCore` that has the new states and events as stubs, and a `Win32Platform` that compiles with a stub per new member in its own partial file so tickets 02 to 07 fill them in without touching each other's files. Everything that exists today keeps working and the test suite stays green (83 tests before this ticket).

## The seam (write it exactly like this; later tickets must not change it)

Namespace `ConsoleSnippingTool.Core`, file-per-type.

```csharp
public enum Signal { Ready, Corner, Done, Sent }   // Sent appended so existing indexes keep their values

/// PCM audio recorded from the microphone: 16-bit little-endian mono samples at SampleRate Hz.
public sealed record AudioClip(byte[] Pcm16, int SampleRate)
{
    public TimeSpan Duration => TimeSpan.FromSeconds(Pcm16.Length / 2.0 / SampleRate);
}

/// What the upload transport needs for one Transcript. ApiKey is never logged.
public sealed record TranscriptionRequest(string ApiKey, string Model, string? Language, string? Prompt);

/// What the Cleanup pass needs. Instructions is the core's constant prompt.
public sealed record CleanupRequest(string ApiKey, string Model, string Instructions);
```

`Hotkey` gains `public static Hotkey DictationDefault => new(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x58, "Ctrl+Win+X");`.

`IPlatform` changes:

```csharp
    // Hotkeys (added)
    bool RegisterDictationHotkey(Hotkey hotkey); // false when another app owns it
    void UnregisterDictationHotkey();

    // Timer (renamed from StartArmedTimer/StopArmedTimer: one timer now serves Armed and Listening)
    void StartTimer(TimeSpan duration);   // restarts if already running
    void StopTimer();

    // Dictation (added)
    void InstallKeyboardHook(Hotkey chord);   // posts OnDictationKeyUp once when any key of the chord goes up; swallows nothing
    void RemoveKeyboardHook();
    bool TryOpenMicrophone();                 // default communication capture device; false when there is none
    AudioClip CloseMicrophone();              // stops capturing and returns everything recorded since TryOpenMicrophone
    void WarmUpService(string apiKey);        // fire-and-forget connection warm-up; result ignored
    void BeginTranscription(int sequence, AudioClip clip, TranscriptionRequest request); // later posts OnTranscript or OnTranscriptionFailed with the same sequence
    void BeginCleanup(int sequence, string transcript, CleanupRequest request);          // later posts OnCleanedTranscript or OnCleanupFailed
    bool TryInsertText(string text, out string reason);   // paste with restore (ADR-0003); reason is set when false
    string? ReadEnvironmentVariable(string name);
```

`Config` gains these init properties with these defaults (parsing and `ToJson` are ticket 02; `SoundFor(Signal.Sent)` returns `SentSound`):

```csharp
public Hotkey DictationHotkey { get; init; } = Hotkey.DictationDefault;
public int ListeningTimeoutSeconds { get; init; } = 120;
public string TranscriptionModel { get; init; } = "gpt-transcribe";
public string? Language { get; init; }
public string? Prompt { get; init; }
public bool Cleanup { get; init; }
public string CleanupModel { get; init; } = "gpt-6-luna";
public string? OpenAiApiKey { get; init; }        // null = read OPENAI_API_KEY from the environment
public SoundSource SentSound { get; init; } = new SoundSource.BuiltIn();
```

`SnipperCore`:

- `State` enum gains `Listening` and `Transcribing`.
- New file `SnipperCore.Dictation.cs` with stubs that do nothing: `OnDictationHotkey()`, `OnDictationKeyUp()`, `OnTranscript(int sequence, string text)`, `OnTranscriptionFailed(int sequence, string reason)`, `OnCleanedTranscript(int sequence, string text)`, `OnCleanupFailed(int sequence, string reason)`. Ticket 03 fills them.
- `SetUpSounds` already iterates every `Signal`, so it now also sets up `Sent`; the existing startup and sound tests that assert the exact call list gain the `TrySetSound(Sent,BuiltIn)` entry (after Done).
- The Capture code calls `StartTimer`/`StopTimer` instead of the old names; nothing else in Capture changes.

`FakePlatform` (test project): records one string per new call in the existing style, never including an API key: `RegisterDictationHotkey(Ctrl+Win+X)`, `UnregisterDictationHotkey()`, `StartTimer(30s)`, `StopTimer()`, `InstallKeyboardHook(Ctrl+Win+X)`, `RemoveKeyboardHook()`, `TryOpenMicrophone()`, `CloseMicrophone()`, `WarmUpService()`, `BeginTranscription(seq:1,clip:3s,model:gpt-transcribe,language:null,prompt:null)`, `BeginCleanup(seq:1,model:gpt-6-luna,transcript:<text verbatim>)`, `TryInsertText(<text verbatim>)`, `ReadEnvironmentVariable(OPENAI_API_KEY)`. Knobs with happy-path defaults: `RegisterDictationHotkeyResult` (true), `MicrophoneOpens` (true), `MicrophoneClip` (an `AudioClip` of 3 s of zeros at 16 kHz), `InsertResult` (true), `InsertFailureReason` ("clipboard locked"), `Environment` (`Dictionary<string, string?>`, empty). Expose `LastTranscriptionRequest` and `LastCleanupRequest` so a test can assert the key and instructions without them appearing in `Calls`. Update the header comment that documents the call formats.

`Win32Platform`: new partial files, one per later ticket, each holding stubs for exactly the members that ticket will implement:

- `Win32Platform.Keyboard.cs` (ticket 04): `RegisterDictationHotkey`/`UnregisterDictationHotkey` are real and mirror the Capture ones with a new `MessageWindow.DictationHotkeyId = 3`; `InstallKeyboardHook`/`RemoveKeyboardHook` are no-ops.
- `Win32Platform.Microphone.cs` (ticket 05): `TryOpenMicrophone` returns false; `CloseMicrophone` returns an empty clip at 16 kHz.
- `Win32Platform.Service.cs` (ticket 06): `WarmUpService` is a no-op; `BeginTranscription` and `BeginCleanup` post the failure event with reason `transport not implemented` through the message window.
- `Win32Platform.Insert.cs` (ticket 07): `TryInsertText` returns false with reason `insert not implemented`.
- `ReadEnvironmentVariable` goes in the main `Win32Platform.cs` and is real (`Environment.GetEnvironmentVariable`).
- The Armed timer field keeps working under the new names.

`MessageWindow`: add `DictationHotkeyId = 3` routed to `OnDictationHotkey()`; `WM_DICTATION_KEY_UP` routed to `OnDictationKeyUp()`; and a thread-safe `Post(Action action)` that enqueues the action and posts a `WM_RUN_POSTED` message whose handler drains the queue on the message loop. Tickets 04 and 06 use these to hand results to the core from other threads.

`NativeMethods` becomes `partial` so later tickets add `NativeMethods.Keyboard.cs`, `NativeMethods.Audio.cs` and `NativeMethods.Input.cs` without editing the shared file.

## Tests

Existing tests updated for the renamed timer calls and the extra `TrySetSound(Sent,BuiltIn)`. New: `SoundFor(Signal.Sent)` is `SentSound`; `AudioClip.Duration` for 3 s of 16 kHz samples is 3 s; `Hotkey.DictationDefault.Text` is `Ctrl+Win+X`; a fresh start still writes the same default file as before (ticket 02 changes it).

## Acceptance

`dotnet build` 0 warnings; `dotnet test` green with every existing test still present; the seam matches the listing above verbatim; no behaviour of Capture changed.

## Comments
- Built test-first on branch `worktree-agent-a12281ed145444ee2` in five `01:` commits; `dotnet build` 0 warnings, `dotnet test` 88 green (83 before plus five: `SoundFor(Sent)`, `Hotkey.DictationDefault`, two `AudioClip.Duration`, the `Config` Dictation defaults; the unchanged fresh-start test already pins the default file). Everything in the listing exists with member signatures and trailing comments verbatim: `Signal.Sent`, `AudioClip`, `TranscriptionRequest`, `CleanupRequest`, `Hotkey.DictationDefault`, the `IPlatform` additions (the Dictation Hotkey pair sits in the existing `Hotkeys` group, the timer in its own `Timer` group, the rest under `Dictation`; the `(added)` labels are not in the file), the nine `Config` properties with `SoundFor(Sent)` (`ToJson`/`Load` untouched), `State.Listening`/`Transcribing`, the six empty stubs in `SnipperCore.Dictation.cs`, `Win32Platform.Keyboard/Microphone/Service/Insert.cs` as described with `ReadEnvironmentVariable` in `Win32Platform.cs`, `MessageWindow.DictationHotkeyId = 3`, `WM_DICTATION_KEY_UP = WM_APP+5`, `WM_RUN_POSTED = WM_APP+6`, `Post(Action)` over a `ConcurrentQueue<Action>`, and `partial` `NativeMethods`. Deviations from the text: none in the seam; the adapter's timer field became `_timer`/`OnTimerTick` and stays in `Win32Platform.Capture.cs`; the `RegisterCancelKey` comment now reads "only while Armed or Listening". Notes for later tickets: the fake's exact `Calls` strings are `RegisterDictationHotkey(<Text>)`, `UnregisterDictationHotkey()`, `StartTimer(<n>s)`, `StopTimer()`, `InstallKeyboardHook(<Text>)`, `RemoveKeyboardHook()`, `TryOpenMicrophone()`, `CloseMicrophone()`, `WarmUpService()`, `BeginTranscription(seq:<n>,clip:<n>s,model:<model>,language:<value or null>,prompt:<value or null>)`, `BeginCleanup(seq:<n>,model:<model>,transcript:<text verbatim>)`, `TryInsertText(<text verbatim>)`, `ReadEnvironmentVariable(<name>)`; knobs `RegisterDictationHotkeyResult`, `MicrophoneOpens`, `MicrophoneClip` (3 s of zeros at 16 kHz, so `clip:3s`), `InsertResult`, `InsertFailureReason` (`clipboard locked`), `Environment`; `LastTranscriptionRequest`/`LastCleanupRequest` hold the full records; `TryInsertText` sets `reason` to `""` when it returns true. Clip durations print as `TimeSpan.TotalSeconds` invariant (`clip:4.2s`), so ticket 03 should pick test clips with short decimals. The Dictation Hotkey is registered with `MOD_NOREPEAT` like the Capture one, so autorepeat `WM_HOTKEY` may never arrive (ticket 04 may revisit; the core still ignores repeats per spec). `TranscriptionRequest`/`CleanupRequest` are plain records whose default `ToString()` prints `ApiKey`: tickets 03 and 06 must never log the record itself. With the current adapter, once ticket 03 lands a Dictation ends in the core's failure path (`transport not implemented`) because the Service stubs post `OnTranscriptionFailed`/`OnCleanupFailed` through `MessageWindow.Post`; ticket 06 replaces them. `ConfigJson.With` in the test project is unchanged for ticket 02 to extend.
