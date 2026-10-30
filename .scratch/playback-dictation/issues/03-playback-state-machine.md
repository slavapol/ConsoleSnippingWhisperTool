# 03 Playback state machine and request timeout

Status: ready-for-agent
Blocked by: 01

Spec: `.scratch/playback-dictation/spec.md` → Core states and transitions, Requests and their timeout (the core's part); user stories 1 to 6, 10 to 24, 29 to 32, 39 and 41. Prior art: `SnipperCore.Dictation.cs` and `DictationTests.cs`.

## Goal

Every Playback transition in the spec is implemented in the core and tested through the fake Platform, and both requests carry the spec's length-based timeout for both Sources.

## Behaviour

Follow the spec's transition list literally. Decided here:

- The core remembers the Source of the Dictation in progress. `_listeningSince` is taken before `TryOpenSource` for both Sources, as today.
- `OnPlaybackHotkey()` replaces ticket 01's placeholder. Playback Listening installs no keyboard hook and registers no cancel key, so leaving it removes no hook: `StopTimer()` then `CloseSource()`.
- The Playback cap is `Config.PlaybackTimeoutSeconds`; the Microphone keeps `ListeningTimeoutSeconds`.
- "Nothing played" is a Playback clip whose `Duration` is under 1000 ms (raised from 500 ms after ticket 04 measured a 0.44 s clip from the Ready click alone), checked after the Tap rule: `Log("Nothing heard (nothing played)")`, Idle, no Sent, no request, detected languages cleared. The Microphone keeps sending every non-Tap Release.
- Timeout: `ceil(20 + clipSeconds / 6)` whole seconds, computed at the Release and kept so the Cleanup request of the same Dictation carries the same value.
- Playback always delivers by Copy and its delivery verb is `copying`, whatever `Config.Insert` says.
- The ignore lines: `Playback hotkey ignored while armed`/`listening`/`transcribing`; the existing `Dictation hotkey ignored while ...` lines become `Microphone hotkey ignored while ...`; the Microphone Hotkey during Playback Listening logs `Microphone hotkey ignored while listening`, while during Microphone Listening it stays silent.
- `OnDictationKeyUp()` and `OnCancelKey()` during Playback Listening do nothing.
- Files: `SnipperCore.Dictation.cs`, `SnipperCore.cs`, `SnipperCore.Capture.cs` if a dispatch needs it, `DictationTests.cs` and a new `PlaybackTests.cs`. Do not touch `Config.cs`, `SnipperCore.Startup.cs`, `SnipperCore.Console.cs` or any Win32 file: tickets 02 and 05 own them.

## Tests

Everything in the spec's Testing Decisions "Covered" list that is core behaviour: the Playback happy path in exact call order; Tap at 499 ms against 500 ms; the cap as a Release with ` (cap)`, never a Tap; 999 ms of audio dropped against 1000 ms sent; Copy with `insert` on and the `copying` fallback line; every cross-job ignore with its line and no Platform calls; stale key-up and Esc during Playback ignored; open failure (`Playback could not be opened`); no API key; Exit while Listening on Playback; the empty-name Playback Listening line; the timeout for 3 s, 60 s and 600 s clips on both requests, for both Sources. Update the existing Dictation tests whose ignore lines or timeouts change; delete none.

## Acceptance

`dotnet build` 0 warnings, `dotnet test` green; `IPlatform` unchanged from ticket 01.

## Comments

### Implemented (2026-10-30)

Built test-first at the spec's one seam: core events in, `FakePlatform` calls and Console lines out. 34 new test cases, 30 in `PlaybackTests.cs` and 4 in `DictationTests.cs`, none deleted. After merging integration at 1ff6948: 256 green, `dotnet build` 0 warnings. `IPlatform` and `FakePlatform` are unchanged.

**Core**

- `SnipperCore.cs`:
  - `_source` (`DictationSource`) is set on entering Listening and kept through Transcribing.
  - `_requestTimeout` holds the request timeout.
  - `OnCancelKey()` discards only while Listening on the Microphone.
- `SnipperCore.Dictation.cs`:
  - `StartListening(DictationSource)` serves both Sources. `CapFor(source)` reads `ListeningTimeoutSeconds` or `PlaybackTimeoutSeconds`. Only the Microphone installs and removes the keyboard hook.
  - `OnPlaybackHotkey()` replaces ticket 01's placeholder.
  - `OnDictationKeyUp()` releases only the Microphone.
  - `RequestTimeoutFor(clip)` is `ceil(20 + seconds / 6)`. `Release` computes it and `BeginCleanup` reuses it. `RequestTimeout` is gone.
  - `Inserting` (Microphone with `insert` on) decides both `Deliver` and the fallback verb.
  - `NothingHeard(line)` takes its line, default `Nothing heard`.

**Call orders**

- Playback press while Idle: `TryOpenSource(Playback)`, `StartTimer(600s)`, `WarmUpService()`, `PlaySound(Ready)`. Log `Listening (playback: <device>)`, or `Listening (playback)` with no name.
- Second press, or the cap: `StopTimer()`, `CloseSource()`, `PlaySound(Sent)`, `BeginTranscription(seq:1,clip:3s,...,timeout:21s)`. Log `Sent 3.0 s of audio`; the timer appends ` (cap)`.
- Tap (under 500 ms from the press): `StopTimer()`, `CloseSource()`. Log `Tap ignored`.
- Under 1 s of audio: `StopTimer()`, `CloseSource()`. Log `Nothing heard (nothing played)`.
- Open failure: `TryOpenSource(Playback)`, `ShowBalloon(ConsoleSnippingTool: Playback could not be opened)`.
- Exit while Listening on Playback: `StopTimer()`, `CloseSource()`, `UnregisterHotkey()`, `UnregisterDictationHotkey()`, `UnregisterPlaybackHotkey()`, `RemoveTrayIcon()`, `Exit()`. Log `Discarded (exit)`.
- Delivery: always `TryCopyText(...)`, `PlaySound(Done)`, `Copied ...`. With `insert` on, the fallback line is still `Cleanup failed: <reason>; copying the raw transcript`.

**Ignored presses (one log line, no Platform call)**

- `Playback hotkey ignored while armed` (Armed, FirstCorner); `... while listening` (Microphone Listening); `... while transcribing` (either Source).
- `Microphone hotkey ignored while armed` and `... while transcribing` replace `Dictation hotkey ...`. `Microphone hotkey ignored while listening` appears only during Playback Listening; during Microphone Listening the press stays silent.
- `Capture hotkey ignored while listening` and `... while transcribing` now cover a Playback Dictation too, with no code change.
- Key-up and Esc during Playback Listening do nothing.

**Timeouts**

- 0.3 s, 0.4 s and 3 s of audio give 21 s; 60 s gives 30 s; 120 s gives 40 s; 600 s gives 120 s. The same value goes on both requests, for both Sources.
- Existing Dictation tests moved from `timeout:20s` to 21 s or 40 s. One test's free-text Cleanup reason now reads `timed out after 21 s`.

**Deviations**

1. The nothing-played threshold is 1000 ms, not the 500 ms this ticket first gave. The coordinator changed it after ticket 04 measured a 0.44 s loopback clip from the Ready click alone. The tests drop 999 ms, send 1000 ms, and drop a 0.44 s clip at the cap.
2. Three tests beyond the list:
   - `The_Playback_cap_comes_from_config_apart_from_the_Microphone_cap` became possible once ticket 02 loaded `playbackTimeoutSeconds`.
   - `A_Release_with_under_half_a_second_of_microphone_audio_is_still_sent` pins the nothing-played rule to Playback.
   - `The_cap_with_under_a_second_of_audio_is_Nothing_heard_too`.
3. I merged integration before writing this comment instead of after, so the comment lands on the coordinator's updated ticket text without a conflict.
4. `The_Playback_cap_is_never_a_Tap` fires the timer at 400 ms with the default 3 s clip, like the Microphone's version. Real loopback cannot record 3 s in 0.4 s, but the rule under test is about the timer, not the clip.
