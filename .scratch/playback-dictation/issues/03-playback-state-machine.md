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
- "Nothing played" is a Playback clip whose `Duration` is under 500 ms, checked after the Tap rule: `Log("Nothing heard (nothing played)")`, Idle, no Sent, no request, detected languages cleared. The Microphone keeps sending every non-Tap Release.
- Timeout: `ceil(20 + clipSeconds / 6)` whole seconds, computed at the Release and kept so the Cleanup request of the same Dictation carries the same value.
- Playback always delivers by Copy and its delivery verb is `copying`, whatever `Config.Insert` says.
- The ignore lines: `Playback hotkey ignored while armed`/`listening`/`transcribing`; the existing `Dictation hotkey ignored while ...` lines become `Microphone hotkey ignored while ...`; the Microphone Hotkey during Playback Listening logs `Microphone hotkey ignored while listening`, while during Microphone Listening it stays silent.
- `OnDictationKeyUp()` and `OnCancelKey()` during Playback Listening do nothing.
- Files: `SnipperCore.Dictation.cs`, `SnipperCore.cs`, `SnipperCore.Capture.cs` if a dispatch needs it, `DictationTests.cs` and a new `PlaybackTests.cs`. Do not touch `Config.cs`, `SnipperCore.Startup.cs`, `SnipperCore.Console.cs` or any Win32 file: tickets 02 and 05 own them.

## Tests

Everything in the spec's Testing Decisions "Covered" list that is core behaviour: the Playback happy path in exact call order; Tap at 499 ms against 500 ms; the cap as a Release with ` (cap)`, never a Tap; 499 ms of audio dropped against 500 ms sent; Copy with `insert` on and the `copying` fallback line; every cross-job ignore with its line and no Platform calls; stale key-up and Esc during Playback ignored; open failure (`Playback could not be opened`); no API key; Exit while Listening on Playback; the empty-name Playback Listening line; the timeout for 3 s, 60 s and 600 s clips on both requests, for both Sources. Update the existing Dictation tests whose ignore lines or timeouts change; delete none.

## Acceptance

`dotnet build` 0 warnings, `dotnet test` green; `IPlatform` unchanged from ticket 01.

## Comments
