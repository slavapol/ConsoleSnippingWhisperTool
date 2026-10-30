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
