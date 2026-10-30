# 01 Seam and skeleton

Status: ready-for-agent
Blocked by: none

Spec: `.scratch/playback-dictation/spec.md` → Platform changes, Testing Decisions (the seam), Requests and their timeout (the adapter's part), Playback Hotkey (the adapter's part). Prior art: `.scratch/dictation/issues/01-seam-and-skeleton.md`.

## Goal

The Platform seam has its final shape for Playback Dictation, every caller and both implementations compile against it, and the behaviour of the shipped tool is unchanged apart from the Microphone's Listening line. Tickets 02, 03 and 05 then work in parallel on separate files without touching the seam again.

## Scope

- Core: a `DictationSource` enum (`Microphone`, `Playback`). `IPlatform`: `TryOpenMicrophone()`/`CloseMicrophone()` become `bool TryOpenSource(DictationSource source, out string deviceName)`/`AudioClip CloseSource()`; add `bool RegisterPlaybackHotkey(Hotkey hotkey)` and `void UnregisterPlaybackHotkey()`. `TranscriptionRequest` and `CleanupRequest` gain `TimeSpan Timeout`; their `ToString()` prints it and still never the key. `AudioClip`'s doc stops saying "microphone".
- `Hotkey.PlaybackDefault` = `Ctrl+Win+A` (VK 0x41). `Config` gains `PlaybackHotkey` (default `Hotkey.PlaybackDefault`) and `PlaybackTimeoutSeconds` (default 600) as plain properties. Do not load them from JSON and do not add them to `ToJson()`: ticket 02 does both.
- Core: `StartListening` opens `TryOpenSource(Microphone, out device)` and logs `Listening (microphone: <device>)`, or `Listening (microphone)` when the name is empty. The core passes a fixed 20 s timeout on both requests for now; ticket 03 replaces it with the spec's formula. Add a public `OnPlaybackHotkey()` that does nothing yet, with a one-line comment that ticket 03 fills it.
- Adapter: the microphone partial becomes the Source partial. `TryOpenSource(Microphone)` opens today's capture with an empty device name; `TryOpenSource(Playback)` returns false with an empty name until ticket 05. `RegisterPlaybackHotkey` registers under a new `MessageWindow` id with `MOD_NOREPEAT`, like the Dictation Hotkey, and the message window dispatches it to `OnPlaybackHotkey()`. The request runner uses each request's `Timeout` for its cancellation and in the `timed out after N s` reason; the warm-up keeps its own fixed 20 s.
- `FakePlatform`: records `TryOpenSource(Microphone)`/`TryOpenSource(Playback)`, `CloseSource()`, `RegisterPlaybackHotkey(<text>)`, `UnregisterPlaybackHotkey()`; the scripted microphone members become source members (opens, the slow-open hook, the clip) plus a device name defaulting to `Test Device`; the recorded request strings include `timeout:20s`. Update its header comment.
- Update every existing test for the renamed calls, the Listening line and the request strings. Add tests for the new request `ToString()` (timeout printed, key absent) and for the empty-name Listening line.

## Acceptance

`dotnet build` 0 warnings, `dotnet test` green with no test deleted. Startup does not register the Playback Hotkey yet (ticket 02).

## Comments

### Implemented (2026-10-30)

Built test-first. Tests went from 197 to 203, none deleted; `dotnet build` has 0 warnings. Startup does not register the Playback Hotkey.

**Seam (`IPlatform`), final for tickets 02 to 04**

- `bool TryOpenSource(DictationSource source, out string deviceName)` and `AudioClip CloseSource()` replace the microphone pair.
- `bool RegisterPlaybackHotkey(Hotkey hotkey)` and `void UnregisterPlaybackHotkey()`.
- `TranscriptionRequest(string ApiKey, string Model, IReadOnlyList<string> Languages, string? Prompt, TimeSpan Timeout)` and `CleanupRequest(string ApiKey, string Model, string Instructions, TimeSpan Timeout)`. Both `ToString()`s end in `, Timeout = 20 s }` and never print the key.
- `Core/DictationSource.cs`: `enum DictationSource { Microphone, Playback }`.

**FakePlatform**

- Recorded strings: `TryOpenSource(Microphone)`, `TryOpenSource(Playback)`, `CloseSource()`, `RegisterPlaybackHotkey(Ctrl+Win+A)`, `UnregisterPlaybackHotkey()`, `BeginTranscription(seq:1,clip:3s,model:gpt-transcribe,languages:en|ru,prompt:null,timeout:20s)` and `BeginCleanup(seq:1,model:gpt-6-luna,timeout:20s,transcript:<text verbatim>)`. In the Cleanup string the timeout comes before the transcript, so the free text stays last.
- Knobs: `SourceOpens` (true), `OpeningSource` (`Action?`, runs inside `TryOpenSource`), `DeviceName` (`"Test Device"`, reported only when the open succeeds, otherwise `""`), `SourceClip` (3 s of zeros at 16 kHz) and `RegisterPlaybackHotkeyResult` (true).
- New `ConsoleReplay.Logged(fake, core)` in `ConsoleReplay.cs`: returns the log ring as Show replays it, skipping the header up to its blank line. `DictationTests.Logged` delegates to it; `PlaybackTests` can use it too.

**Core**

- `Hotkey.PlaybackDefault`: `Ctrl+Win+A`, VK 0x41.
- `Config.PlaybackHotkey` (default `Hotkey.PlaybackDefault`) and `Config.PlaybackTimeoutSeconds` (default 600) are plain init properties. They are not loaded from JSON and not in `ToJson()`.
- In `SnipperCore.Dictation.cs`:
  - `public void OnPlaybackHotkey()` is empty.
  - `private static readonly TimeSpan RequestTimeout` (20 s) goes on both requests; ticket 03 replaces it.
  - `ListeningLine(string source, string deviceName)` gives `Listening (microphone: <device>)`, or `Listening (microphone)` when the name is empty. Ticket 03 calls it with `"playback"`.
  - `StopListening()` still removes the keyboard hook, so ticket 03 has to keep that call out of the Playback path.
- The Microphone's failure line is still `No microphone found`.

**Adapter**

- `Win32/Win32Platform.Source.cs` (formerly `Win32Platform.Microphone.cs`), with field `_source` and const `SourceSampleRate` = 16000:
  - `TryOpenSource(Microphone)` opens today's `WasapiCapture.TryOpen(16000)`.
  - `TryOpenSource(Playback)` returns false.
  - `deviceName` is always `""`.
- `MessageWindow.PlaybackHotkeyId = 4` sends its `WM_HOTKEY` to `OnPlaybackHotkey()`. `RegisterPlaybackHotkey`/`UnregisterPlaybackHotkey` sit in `Win32Platform.Keyboard.cs` next to the Dictation Hotkey and use `MOD_NOREPEAT`.
- `Win32Platform.Service.cs`: `Begin` uses the request's `Timeout` for its `CancellationTokenSource` and in `timed out after N s`. The warm-up keeps `WarmUpTimeout` (20 s).
- For ticket 04: `WasapiCapture.TryOpen(int sampleRate)` is unchanged, and the drain thread is still named `Microphone drain`.

**Deviations**

1. Exit now calls `UnregisterPlaybackHotkey()` right after `UnregisterDictationHotkey()`, which was ticket 02's scope. Six tests check the exact Exit call list, in `CaptureTests`, `DictationTests`, `StartupTests` and `ConsoleTests`. Ticket 02 may not edit the first two, and ticket 03 writes new Exit tests at the same time, so the list is fixed here. Unregistering an id that was never registered does nothing. I renamed `Exit_while_Idle_releases_both_Hotkeys_…` to `…_every_Hotkey_…`. Ticket 02 still owns the registration at startup and its test.
2. `DictationTests.Logged` used to skip a fixed 5 header lines and now uses `ConsoleReplay.Logged`. Ticket 02 adds a header line and may not edit `DictationTests.cs`. I added the Playback header line temporarily to check: only `ConsoleTests` failed.
3. This ticket says Playback stays unopened "until ticket 05" and that 02, 03 and 05 run in parallel. The loopback capture is ticket 04, so the code comments say ticket 04.
4. I added two default tests to `ConfigTests.cs`: the Playback defaults, and that `Hotkey.PlaybackDefault` equals the parsed `"Ctrl+Win+A"`.
5. I added the `RegisterPlaybackHotkeyResult` knob, which this ticket does not list, because ticket 02 needs a taken Playback Hotkey and does not own `FakePlatform.cs`.
