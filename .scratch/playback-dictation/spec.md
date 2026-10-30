# Playback Dictation

Status: ready-for-agent

Agreed in a grilling session on 2026-10-30; every recommended answer was accepted. Glossary terms are in `GLOSSARY.md`: Source, Microphone and Playback are new, and Dictation, Listening, Release, Tap, Discard, Copy, Insert, Transcript and Hotkey were widened for two Sources. ADR-0005 records why Playback is recorded from the communication output device and not from the meeting app. ADR-0004 (upload after the Release, not streaming) still governs the request; its size note now covers the 600-second Playback cap. The Dictation spec in `.scratch/dictation/spec.md` remains the reference for everything this spec does not change.

## Problem Statement

In Teams and Zoom meetings the user wants what the other people say as text: to quote a decision into their notes, paste an answer into a ticket, or reread something they missed. Dictation on `Ctrl+Win+X` hears only the user's own microphone. The meeting apps' own transcripts have to be switched on by the organiser, live inside the meeting app, and are often available only after the meeting. The user wants the shape Dictation already has, invisible and driven by one Hotkey with sounds as the only feedback, but for what the computer is playing: press a key, let the meeting talk, press again, and the words are on the clipboard.

## Solution

A second Source for Dictation. Pressing `Ctrl+Win+A` opens Listening on Playback, the sound the computer plays on its Default Communication Device, and the Ready signal plays. The user lets go of the keys and keeps working. Pressing `Ctrl+Win+A` again is the Release: the Sent signal plays, the audio goes to the same transcription endpoint as a Microphone Dictation, and the Transcript lands on the clipboard with the Done signal. A Playback Dictation always ends in Copy, whatever `dictation.insert` says. Listening ends by itself at the Playback cap, ten minutes by default, and sends what it heard. Two presses within half a second are a Tap. Esc is left to the user's apps. A recording in which nothing played is dropped without a request. The Console's Listening line names the device being recorded, for both Sources, so a meeting that plays on another speaker is easy to spot. Both Sources share the model, the languages, the prompt and Cleanup; the request timeout now grows with the length of the recording, for both Sources.

## User Stories

### Starting and ending a Playback Dictation

1. As a user in a meeting, I want to press `Ctrl+Win+A` once and hear the Ready signal, so that I know the tool has started hearing the meeting.
2. As a user, I want to let go of `Ctrl+Win+A` straight after pressing it and keep both hands free while Listening, so that I can type notes or click around during a long stretch of speech.
3. As a user, I want a second press of `Ctrl+Win+A` to end Listening with the Sent signal, so that I decide where the passage ends.
4. As a user, I want the Transcript of what the others said on the clipboard with the Done signal, so that I can paste it into my notes, a ticket or a chat.
5. As a user, I want the Transcript as one block of plain text, so that it pastes cleanly anywhere.
6. As a user, I want each person's words in the language they spoke, with my configured languages as the hint, so that a meeting that mixes English and Russian comes out right.
7. As a user, I want the same Hotkey to work for Teams, Zoom and a meeting in a browser tab, so that I never have to tell the tool which app is playing.
8. As a user, I want my own voice left out of a Playback Dictation, so that the Transcript is what the others said; a meeting app does not play my voice back to me.
9. As a user, I want nothing drawn on screen at any point, so that a screen share shows nothing of the tool.
10. As a user, I want holding `Ctrl+Win+A` down to count as one press, so that a slow key release never sends early.

### Taps, the cap and stopping

11. As a user, I want a second press within half a second of the first to send nothing and play no sound, so that an accidental double press costs nothing.
12. As a user, I want Listening on Playback to end by itself after ten minutes and send what it heard, so that a forgotten Playback Dictation never runs for the rest of the meeting and the ten minutes are not lost.
13. As a user, I want to set the Playback cap separately from the Microphone cap, so that short voice notes and long meeting passages each get a fitting limit.
14. As a user, I want the cap to end the Playback Dictation, not start another one, so that the tool never records more than I asked for.
15. As a user, I want Esc to keep working in my apps during a Playback Dictation, so that closing a dialog or leaving full screen in Zoom neither fails nor throws the recording away.
16. As a user, I want Exit and a relaunch to discard a Playback Dictation in progress and close the device, so that closing the tool never leaves a recording running.
17. As a user, I want a Playback recording in which nothing played to be dropped without a request, with the Console line `Nothing heard (nothing played)`, so that a silent device costs nothing and I can see why no Sent signal came.

### Delivery

18. As a user with `dictation.insert` on, I want a Playback Transcript still to go to the clipboard only, so that a long transcription that lands a minute later is never pasted into the meeting chat or whatever has focus by then.
19. As a user, I want a Playback Transcript to replace the clipboard as any Ctrl+C would, so that pasting it does what I expect.
20. As a user, I want a Playback Dictation in which nobody spoke to deliver nothing and play no Done signal, so that silence never replaces my clipboard with a blank line.
21. As a user with Cleanup on, I want a Playback Transcript cleaned the same way as a Microphone one, so that the meeting's fillers and false starts are dropped too.
22. As a user, I want a failed Cleanup of a Playback Transcript to fall back to the raw Transcript on the clipboard, with a Console line that says it is copying, so that a cleanup problem never loses the meeting text.

### One job at a time

23. As a user, I want `Ctrl+Win+Z` and `Ctrl+Win+X` ignored with a Console line while a Playback Dictation is Listening or Transcribing, so that two jobs never interleave or fight over the clipboard.
24. As a user, I want `Ctrl+Win+A` ignored with a Console line while a Capture is Armed, while a Microphone Dictation is Listening, and while any Transcript is still in flight, so that Dictations never overlap or arrive out of order.

### The device, and seeing which one

25. As a user, I want Playback to record the Windows Default Communication Device, so that a Bluetooth headset in a call, which plays the meeting on its Hands-Free device, is heard.
26. As a user, I want the device opened on each first press and closed at the Release, so that a changed default is picked up next time and nothing is recorded while Idle.
27. As a user, I want the Console's Listening line to name the device being recorded, for Playback and for the Microphone, so that I can see at once when the meeting plays somewhere else.
28. As a user, I want a balloon and a Console line when the playback device cannot be opened, with no Ready signal, so that I know why nothing started.

### Timeouts

29. As a user, I want the transcription request's timeout to grow with the length of the recording, so that a ten-minute passage is not cut off at 20 seconds.
30. As a user, I want the Cleanup of that Dictation to get the same longer timeout, so that cleaning ten minutes of text is not cut off either.
31. As a user, I want the failure line to name the limit that applied, such as `timed out after 120 s`, so that I know how long it waited.
32. As a Microphone user, I want a short Dictation to keep a timeout close to today's 20 seconds, so that a dead network is still reported quickly.

### Configuration

33. As a user, I want to set the Playback Hotkey as a string such as `Ctrl+Alt+M` in `dictation.playbackHotkey`, so that it never clashes with my other tools.
34. As a user, I want a Playback Hotkey equal to the Capture or the Microphone Hotkey reset to its default with a balloon, so that every job stays reachable.
35. As a user, I want an invalid `dictation.playbackHotkey` or `dictation.playbackTimeoutSeconds` to fall back to its default with one balloon and one Console line, as every other field does.
36. As a user with an existing `config.json`, I want my file to keep working unchanged, with `dictation.hotkey` still meaning the `Ctrl+Win+X` job, so that the upgrade costs nothing.
37. As a user, I want the default `config.json` written on first start to show the two new fields, so that I can find them.
38. As a user, I want a Playback Hotkey that another app already owns reported at startup while Capture and Microphone Dictation keep working.
39. As a user, I want the model, the languages, the prompt and the Cleanup settings to apply to both Sources, so that I configure transcription once.

### Console, tray and README

40. As a user, I want the Console header and the tray tooltip to list `Ctrl+Win+A`, so that I can find the new Hotkey without the README.
41. As a user, I want the Console lines of a Playback Dictation (sent, copied, nothing heard, failed) to read like the Microphone ones, so that one log tells the whole story.
42. As a user, I want the README to document the two new settings and how a Playback Dictation works, so that I can use it without reading code.
43. As a user, I want the README to say that a Playback Dictation sends other people's voices to OpenAI and that some places require their consent to record, so that I use it responsibly.
44. As a user, I want the README's troubleshooting section to tell me what to check when Playback hears nothing, so that I compare the meeting app's speaker setting with the device on the Listening line.

### Developer

45. As a developer, I want the Source to be a parameter of the existing Platform calls rather than a second set of calls, so that the core stays one state machine and the fake stays small.
46. As a developer, I want every Playback rule tested through the fake Platform, as the Microphone rules are, so that the behaviour is pinned without Windows.
47. As a developer, I want the request timeout computed in the core and carried on the requests, so that the adapter keeps making no decisions and the rule is tested.

## Implementation Decisions

### Core states and transitions

- No new state. Listening and Transcribing serve both Sources; the core remembers the Source of the Dictation in progress. The core gains a `DictationSource` enum with `Microphone` and `Playback`, named so to stay apart from the existing `SoundSource`.
- New core event `OnPlaybackHotkey()`. The cap still arrives as `OnTimerElapsed()`.
- Transitions for the Playback Hotkey:
  - Idle, no API key: Report the existing no-key message, stay Idle, no sound.
  - Idle: the press is timed with the injected clock before the device opens, as for the Microphone. `TryOpenSource(Playback, out device)`; false means Report `Playback could not be opened` and Idle. True means `StartTimer(Playback cap)`, `WarmUpService(key)`, `PlaySound(Ready)`, Listening, log `Listening (playback: <device>)`. No keyboard hook is installed and no cancel key is registered.
  - Listening on Playback: the Release. `StopTimer()`, clip = `CloseSource()`, Idle. Under 500 ms from the first press: log `Tap ignored`, no sound. Otherwise continue as described under "After a Playback Release".
  - Listening on the Microphone: log `Playback hotkey ignored while listening`.
  - Armed or FirstCorner: log `Playback hotkey ignored while armed`.
  - Transcribing, either Source: log `Playback hotkey ignored while transcribing`.
- The timer while Listening on Playback is a Release by the cap and is never a Tap, as for the Microphone.
- After a Playback Release that is not a Tap: a clip with less than 1 s of audio is not sent. Log `Nothing heard (nothing played)`, Idle, no Sent, no request. Loopback delivers nothing while no app plays, but the 30 ms built-in Ready click lands in the recording whenever the Default Device is also the communication default, and the sound player renders about 0.4 s of silence after it, so the click alone makes a 0.44 s clip (measured in ticket 04) and "empty" would almost never be true. Otherwise `PlaySound(Sent)`, `BeginTranscription`, Transcribing, log `Sent 312.4 s of audio` with ` (cap)` appended when the timer ended it. The Microphone keeps its current rule: it sends every Release that is not a Tap.
- While Listening on Playback, `OnDictationKeyUp()` and `OnCancelKey()` are ignored. Windows sends neither then, because no hook is installed and no cancel key is registered; ignoring them keeps a stale message harmless.
- The Microphone Hotkey while Listening on Playback logs `Microphone hotkey ignored while listening`. While Listening on the Microphone it stays silent, as today, because it can only be autorepeat. The existing ignore lines that say `Dictation hotkey ignored while ...` become `Microphone hotkey ignored while ...`, since there are now two Dictation Hotkeys. The Capture Hotkey lines are unchanged and now also apply during a Playback Dictation.
- Delivery: a Playback Transcript is always Copied. A Microphone Transcript is Copied or Inserted by `dictation.insert`, as today. The Cleanup fallback line names the delivery that will happen, so for Playback it always reads `Cleanup failed: <reason>; copying the raw transcript`. Nothing heard, Cleanup `EMPTY`, failures and late results behave as for the Microphone.
- Exit: unchanged. Listening on either Source is discarded with `Discarded (exit)` and the device is closed; a Transcription in flight is abandoned.
- The Microphone's Listening line becomes `Listening (microphone: <device>)`. When the adapter reports an empty device name, the line is `Listening (microphone)` or `Listening (playback)`.

### Playback Hotkey

- Registered with `RegisterHotKey` under its own id with `MOD_NOREPEAT`, and dispatched by the message window as `OnPlaybackHotkey()`. Only the press matters, so no key-up and no hook are involved. Default `Ctrl+Win+A`. Windows 11 uses `Win+A` for Quick Settings and documents no shortcut for `Ctrl+Win+A`; a conflict with another app is reported at startup through the existing `Hotkey ... is already taken by another app; edit config.json and relaunch` path while the other two Hotkeys keep working.
- The clash rule mirrors the existing one. The written `dictation.playbackHotkey` is compared against the Capture Hotkey in effect and the Microphone Hotkey in effect, each after its own fallback. An equal one is reset to the default with `config.json: dictation.playbackHotkey Ctrl+Win+X is the same as the Microphone Hotkey in effect; using default Ctrl+Win+A`, or `Capture Hotkey` in place of `Microphone Hotkey`. A missing or invalid field is not compared. When the default itself then collides, the registration fails with `ERROR_HOTKEY_ALREADY_REGISTERED` and is reported as taken.
- Startup registers it after the Microphone Hotkey; Exit unregisters it.

### Playback capture

- The existing WASAPI capture, parameterised by Source. The Microphone opens the capture endpoint of the communications role, as today. Playback opens the render endpoint of the communications role with the loopback stream flag. Everything else is shared: shared mode, `AUTOCONVERTPCM | SRC_DEFAULT_QUALITY | EVENTCALLBACK`, 16 kHz 16-bit mono, the half-second engine buffer, the drain thread and the in-memory buffer, opened on the press and closed at the Release.
- Loopback delivers packets only while something renders to the endpoint. The drain loop's existing 100 ms wait means it never stalls. The gaps are not filled, so the clip holds only what played and can be shorter than the time spent Listening. The 100 ms wake also keeps the drain correct on systems where a loopback stream never signals its event.
- `AUTOCONVERTPCM` on a loopback stream is reported to work, but Microsoft does not document it. Should `Initialize` reject it on some PC, `TryOpenSource` is false and the user sees `Playback could not be opened`. A fallback that captures the mix format and converts in code is a follow-up, not part of this spec; the end-to-end ticket's manual check confirms the flag on the user's PC.
- The device name is the endpoint's friendly name, read from its property store (`PKEY_Device_FriendlyName`) when the device opens, for both Sources. It is empty when it cannot be read. This needs `IPropertyStore` and a `PROPVARIANT` declared with the existing COM interfaces, released with `PropVariantClear`.
- The Signals play through the existing sound player on the Default Device. When that is also the communication default, the Ready click is in the recording; ADR-0005 accepts this.

### Requests and their timeout

- The timeout is 20 s plus 10 s per minute of the clip's audio, rounded up to a whole second: 3 s of audio gives 21 s, one minute gives 30 s, the 120-second Microphone cap gives 40 s, and ten minutes give 120 s. The core computes it at the Release and carries it on the `TranscriptionRequest` and on the `CleanupRequest` of the same Dictation.
- The adapter uses the request's timeout for its cancellation and in the failure reason, `timed out after 120 s`. The warm-up keeps its fixed 20 s.
- Both request records print their timeout in `ToString()` and still never the key.
- Unchanged: the endpoint, the multipart form, `languages[]`, the prompt, the model, no retry, the one shared client, the warm-up on entering Listening. For a Playback Dictation longer than a minute the pooled connection idles out before the Release and the request reconnects, a fraction of a second against a minute of transcription.

### Configuration

- Two new fields in `dictation`, with the existing per-field fallback and one balloon plus Console line per problem. The `dictation` section of the default file becomes:

  ```json
  "dictation": {
    "hotkey": "Ctrl+Win+X",
    "listeningTimeoutSeconds": 120,
    "playbackHotkey": "Ctrl+Win+A",
    "playbackTimeoutSeconds": 600,
    "insert": false,
    "model": "gpt-transcribe",
    "languages": ["en", "ru"],
    "prompt": null,
    "cleanup": false,
    "cleanupModel": "gpt-6-luna"
  }
  ```

- `playbackHotkey`: a Hotkey string, default `Ctrl+Win+A`. `playbackTimeoutSeconds`: an integer from 1 to 600, default 600; ten minutes of 16 kHz mono is about 19 MB, under the endpoint's 25 MB limit.
- `hotkey` and `listeningTimeoutSeconds` keep their names and now mean the Microphone Hotkey and the Microphone cap. `insert` applies to the Microphone only. `model`, `languages`, `prompt`, `cleanup` and `cleanupModel` apply to both Sources. No field is renamed, so existing files load unchanged.

### Console and tray

- Header: `ConsoleSnippingTool`, `Press Ctrl+Win+Z to capture`, `Hold Ctrl+Win+X to dictate`, `Press Ctrl+Win+A to transcribe playback, again to send`, `Esc or right-click cancels a capture; Esc discards a microphone dictation`.
- Tooltip: `ConsoleSnippingTool (capture Ctrl+Win+Z, dictate Ctrl+Win+X, playback Ctrl+Win+A)`. It stays under the tray's 127-character limit even with three long Hotkeys.
- The log ring, Show and Hide are unchanged.

### Platform changes

- `bool TryOpenMicrophone()` and `AudioClip CloseMicrophone()` become `bool TryOpenSource(DictationSource source, out string deviceName)` and `AudioClip CloseSource()`. The clip's documentation stops saying it comes from the microphone.
- `bool RegisterPlaybackHotkey(Hotkey hotkey)` and `void UnregisterPlaybackHotkey()`.
- `TranscriptionRequest` and `CleanupRequest` gain `TimeSpan Timeout`.
- The adapter still makes no decisions: it opens the Source the core names, reports the device name it found, and applies the timeout it was given.

### README

- The config reference gains the two fields, notes that `insert` applies to the Microphone only and that the other dictation settings are shared, and shows the new default file.
- A short section on using a Playback Dictation: press, press again, the cap, Copy only, Esc left alone.
- A consent note: a Playback Dictation sends other people's voices to OpenAI, and some places require everyone's consent to record.
- Troubleshooting: when Playback hears nothing, compare the device on the Listening line with the speaker chosen in the meeting app, and make that device the Default Communication Device or pick the default in the app.

## Testing Decisions

- One seam, and it is the existing one: the Platform interface between the core and the Win32 adapter, exercised through the recording fake Platform. No new seam is introduced. The seam widens by the Source and the device name on open, which replace the microphone pair; by the Playback Hotkey registration pair; and by the timeout on both requests. The core gains one event, `OnPlaybackHotkey()`.
- A good test drives the core through its public events and asserts only on the fake's recorded calls, their order and the Console lines; never on state fields, never on Windows.
- `FakePlatform` records `TryOpenSource(Microphone)` or `TryOpenSource(Playback)` and `CloseSource()`, scripts the open result, the device name and the clip, records `RegisterPlaybackHotkey(Ctrl+Win+A)`, and prints the timeout in its recorded requests, for example `timeout:21s`.
- Prior art: the Dictation tests (hold, Tap, cap, Esc, cross-job ignores, Copy and Insert, Cleanup, late results, Exit), the Config tests (per-field fallbacks and the equal-Hotkeys rule), the Startup tests (registration and a taken Hotkey), the Console tests (header and tooltip), the shared config.json builder, and the request tests that check `ToString()` leaves out the key.
- Covered:
  - the Playback happy path: open with the Source, timer with the Playback cap, warm-up, Ready, the Listening line with the device, no hook; on the second press timer off, device closed, Sent, the request with its timeout; then Copy, Done and the log line;
  - a Tap under 500 ms against exactly 500 ms and over; the cap as a Release with ` (cap)`, never a Tap;
  - under 1 s of audio dropped with `Nothing heard (nothing played)` and no request, against 1 s sent;
  - Copy with `dictation.insert` on; the Cleanup fallback line saying `copying`;
  - every cross-job ignore with its line and no Platform calls: Playback Hotkey in Armed, FirstCorner, Microphone Listening and Transcribing; Microphone Hotkey in Playback Listening; Capture Hotkey in Playback Listening and Transcribing;
  - a stale key-up or Esc during Playback Listening ignored;
  - the device failing to open; no API key; Exit while Listening on Playback;
  - the device name on both Listening lines, and the line without a name;
  - the timeout on both requests for a 3-second, a one-minute and a ten-minute clip, and the Cleanup request carrying the timeout of its Dictation;
  - config loading of both new fields, their fallbacks and both clash rules, and the new default file;
  - registration and a taken Playback Hotkey at startup; unregistration on Exit;
  - the header and tooltip text.
- Existing tests whose expected text changes are updated, not deleted: the Listening line, the `Dictation hotkey ignored` lines, the microphone call names, the recorded request strings, the header, the tooltip and the default file.
- The loopback capture, the property store and the Hotkey registration are adapter code and are not unit tested. The end-to-end ticket records a manual run:
  - a Teams or Zoom test call on laptop speakers, and again with a Bluetooth headset;
  - a video playing in a browser;
  - nothing playing at all;
  - Esc in a full-screen Zoom during Listening;
  - `Ctrl+Win+Z` during Listening;
  - letting the cap end a Playback Dictation;
  - `dictation.insert` on;
  - the device names on both Listening lines;
  - a loopback stream that accepts the 16 kHz mono format.

## Out of Scope

- Recording a whole meeting: continuous capture, splitting it into chunks, restarting after the cap, writing transcripts to a file.
- Speaker labels or diarisation.
- Mixing the Microphone into Playback.
- Recording one app's audio (process loopback), choosing a device, or following the plain Default Device (ADR-0005).
- Keeping the tool's own Signals or notification sounds out of the recording.
- Running a Capture and a Dictation at the same time, or starting a Dictation while another is Transcribing.
- Any visual indicator while Listening on Playback.
- Insert for Playback.
- Streaming audio and live partial text (ADR-0004).
- A consent prompt in the app.
- A Cleanup instruction written for meetings.
- A fallback for PCs whose loopback stream rejects the automatic format conversion.

## Further Notes

- Facts checked on 2026-10-30:
  - Loopback delivers no data while nothing renders to the endpoint (https://github.com/naudio/NAudio/blob/master/Docs/WasapiLoopbackCapture.md).
  - The NAudio author reports `AUTOCONVERTPCM` working for 16 kHz mono capture, loopback included (https://markheath.net/post/2022/4/30/wasapi-sample-rate-conversion); Microsoft documents the flags but not that combination (https://learn.microsoft.com/en-us/windows/win32/coreaudio/audclnt-streamflags-xxx-constants).
  - Teams keeps its own speaker setting and does not reliably follow either Windows default (https://techcommunity.microsoft.com/discussions/microsoftteams/default-audio-devices-in-teams/1649633).
- Process loopback needs Windows 11 and activates through `ActivateAudioInterfaceAsync`; ADR-0005 explains why it was rejected.
- `main` (c98ea5a) carries Capture and Dictation; this feature is built on a new branch from it.
