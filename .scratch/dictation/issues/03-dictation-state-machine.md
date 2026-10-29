# 03 Dictation state machine

Status: ready-for-agent
Blocked by: 01, 02

Spec: `.scratch/dictation/spec.md` → Core states and transitions, Hotkey release and Tap (the core's part: the 500 ms rule), Cleanup (the instructions text), Insert (the core's part), user stories 1 to 7, 10 to 20, 23, 30 to 33, 35, 40, 41. Prior art: `SnipperCore.Capture.cs` and `CaptureTests.cs`.

## Goal

Every Dictation transition in the spec is implemented in `SnipperCore.Dictation.cs` and tested through the fake Platform in a new `DictationTests.cs`. The cross-job rules touch `SnipperCore.Capture.cs` (`OnHotkey`, `OnCancelKey`, `OnTimerElapsed` dispatch by state) and `SnipperCore.Startup.cs` (`OnExit`). Do not touch `Config.cs`, the console file or any Win32 file.

## Behaviour

Follow the spec's transition list literally. Points that need a decision are decided here:

- The core keeps `_dictationSequence` (starts at 0, incremented when a request is sent), `_listeningSince` and `_sentAt` (from the injected clock), and the raw Transcript while a Cleanup is in flight.
- `OnDictationHotkey` in Idle: no key → `Report` with the spec's text, stay Idle. Else `TryOpenMicrophone()`; false → `Report("No microphone found")`, Idle. True → `StartTimer(listening cap)`, `RegisterCancelKey()`, `InstallKeyboardHook(dictation hotkey)`, `WarmUpService(key)`, `PlaySound(Ready)`, state Listening, `Log("Listening")`. In Listening: ignored silently (autorepeat). In Transcribing: `Log("Dictation hotkey ignored while transcribing")`. In Armed or FirstCorner: `Log("Dictation hotkey ignored while armed")`.
- `OnHotkey` (Capture) in Listening or Transcribing: `Log("Capture hotkey ignored while listening")` / `("... while transcribing")`, nothing else. Its existing behaviour in the other states is unchanged.
- `OnDictationKeyUp` in Listening: `RemoveKeyboardHook()`, `UnregisterCancelKey()`, `StopTimer()`, `CloseMicrophone()`. Held less than 500 ms → `Log("Tap ignored")`, Idle. Else sequence++, `PlaySound(Sent)`, `BeginTranscription(sequence, clip, request)`, Transcribing, `Log($"Sent {seconds:0.0} s of audio")`. In any other state: ignored.
- `OnTimerElapsed` in Listening: same as key-up except the log line ends with ` (cap)` and the Tap rule does not apply. In Armed/FirstCorner: unchanged.
- `OnCancelKey` in Listening: `RemoveKeyboardHook()`, `UnregisterCancelKey()`, `StopTimer()`, `CloseMicrophone()` (clip dropped), Idle, `Log("Discarded (Esc)")`. In Armed/FirstCorner: unchanged.
- `OnTranscript(sequence, text)`: ignored unless Transcribing and sequence is current. Whitespace-only → `Log("Nothing heard")`, Idle. Cleanup off → Insert. Cleanup on → remember the raw text, `BeginCleanup(sequence, text, new CleanupRequest(key, cleanupModel, Instructions))`, stay Transcribing.
- `OnCleanedTranscript(sequence, text)`: same guard. `EMPTY` (trimmed, case-sensitive) or whitespace → `Log("Nothing heard")`, Idle. Else Insert.
- `OnCleanupFailed(sequence, reason)`: same guard. `Log($"Cleanup failed: {reason}; inserting the raw transcript")`, Insert the raw text.
- `OnTranscriptionFailed(sequence, reason)`: same guard. `Report($"Dictation failed: {reason}")`, Idle.
- Insert(text): `TryInsertText(text, out reason)`; true → `PlaySound(Done)`, `Log($"Inserted {text.Length} chars in {wait:0.0} s: {oneLine}")` where wait is now minus `_sentAt` and `oneLine` replaces `\r\n` and `\n` with the two characters `\n`; false → `Report($"Transcript could not be inserted: {reason}; nothing was pasted")`. Idle either way.
- `OnExit` in Listening: the discard sequence with `Log("Discarded (exit)")`; in Transcribing: sequence++ so a late result is ignored, `Log("Dictation abandoned (exit)")`; then the existing Exit steps.
- `TranscriptionRequest` is built from the resolved key and `Config.TranscriptionModel`, `Language`, `Prompt`. The Cleanup `Instructions` constant is the spec's text verbatim.

## Tests (DictationTests.cs)

Use a settable clock (a field the test advances) so the Tap threshold and the wait can be asserted. Cover, asserting the exact `Calls` order where it matters: happy path from press to Done with the log lines; Tap at 499 ms versus 500 ms; cap behaving as a Release with ` (cap)`; Esc discard; autorepeat in Listening causes no calls; Dictation Hotkey in Armed, FirstCorner, Transcribing; Capture Hotkey in Listening and Transcribing; left and right clicks in Listening are not consumed; no API key → balloon, no microphone call; microphone fails → balloon; transcription failed → balloon, no Done; empty Transcript → Nothing heard; Cleanup on → `BeginCleanup` with the instructions and then Done after `OnCleanedTranscript`; Cleanup returning `EMPTY`; Cleanup failed → raw text inserted and log line; insert failed → balloon, no Done; a result with a stale sequence ignored; a result while Idle ignored; Exit while Listening (microphone closed, log) and while Transcribing (late result ignored afterwards); the request carries language and prompt from config (via `LastTranscriptionRequest`) and the key never appears in `Calls`.

## Acceptance

`dotnet test` green; `IPlatform` unchanged; all Capture tests still pass unchanged except where the timer rename from ticket 01 already touched them.

## Comments
