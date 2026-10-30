# 02 Config, startup and Console

Status: ready-for-agent
Blocked by: 01

Spec: `.scratch/playback-dictation/spec.md` → Configuration, Playback Hotkey (the clash rule and startup), Console and tray; user stories 33 to 38 and 40. Prior art: `.scratch/dictation/issues/02-config-and-startup.md`, `ConfigTests.cs`, `StartupTests.cs`, `ConsoleTests.cs`.

## Goal

`dictation.playbackHotkey` and `dictation.playbackTimeoutSeconds` are read, validated and written exactly as the spec says; the Playback Hotkey is registered at startup and unregistered on Exit; the Console header and the tray tooltip list it.

## Scope

- `Config.Load`: both fields with the existing per-field fallback and message shapes; `playbackTimeoutSeconds` is an integer from 1 to 600. The clash rule compares the written `dictation.playbackHotkey` against the Capture Hotkey in effect and then the Microphone Hotkey in effect, each after its own fallback; an equal one is reset to the default with the spec's message (`... is the same as the Microphone Hotkey in effect; using default Ctrl+Win+A`, or `Capture Hotkey`). A missing or invalid field is not compared.
- `ToJson()`: the two fields after `listeningTimeoutSeconds`, in the order of the spec's default file.
- Startup registers the Playback Hotkey after the Microphone Hotkey, reporting a taken one through the existing `HotkeyTaken` message; `OnExit` unregisters it after the Dictation Hotkey.
- Console header and tooltip exactly as the spec's Console and tray section.
- Files: `Config.cs`, `SnipperCore.Startup.cs`, `SnipperCore.Console.cs` and their tests, plus the shared config.json builder in the test project. Do not touch `SnipperCore.Dictation.cs`, `SnipperCore.cs`, `SnipperCore.Capture.cs`, `DictationTests.cs` or any Win32 file: tickets 03 and 05 own them.

## Acceptance

`dotnet build` 0 warnings, `dotnet test` green. Tests cover both fields valid, missing, invalid and out of range; both clash cases; the default-collision case (Capture set to `Ctrl+Win+A`, Playback omitted) ending as a taken Hotkey at startup; the new default file; registration order and the taken-Hotkey report; unregistration on Exit; the header and tooltip text.

## Comments

### Implemented (2026-10-30)

Built test-first. Tests went from 203 to 222, none deleted; `dotnet build` has 0 warnings. Only the ticket's files changed.

**Config**

- `Load` reads `dictation.playbackHotkey` and `dictation.playbackTimeoutSeconds` (1 to 600) right after `listeningTimeoutSeconds`, so their problems come in that file order. Messages:
  - `config.json: dictation.playbackHotkey is missing; using default Ctrl+Win+A`
  - `config.json: dictation.playbackHotkey "Ctrl+Home" is not a valid Hotkey; using default Ctrl+Win+A`
  - `config.json: dictation.playbackTimeoutSeconds is missing; using default 600`
  - `config.json: dictation.playbackTimeoutSeconds 601 is not an integer from 1 to 600; using default 600`
- Clash rule: a written, valid `playbackHotkey` is compared with the Capture Hotkey in effect, then with the Microphone Hotkey in effect. The first match adds one problem and resets it:
  - `config.json: dictation.playbackHotkey Ctrl+Alt+M is the same as the Capture Hotkey in effect; using default Ctrl+Win+A`
  - `... is the same as the Microphone Hotkey in effect; ...`
  
  "In effect" means after that Hotkey's own fallback, including a Microphone Hotkey reset by its own clash. A missing or invalid `playbackHotkey` is not compared.
- `ToJson()` writes `"playbackHotkey": "Ctrl+Win+A"` and `"playbackTimeoutSeconds": 600` after `listeningTimeoutSeconds`.

**Startup**

- `Start` calls `RegisterPlaybackHotkey(<hotkey>)` right after `RegisterDictationHotkey`. When it returns false, the core reports `Hotkey Ctrl+Win+A is already taken by another app; edit config.json and relaunch` and goes on to set up the tray. The default-collision case (Capture `Ctrl+Win+A`, Playback omitted) gives the missing-field balloon and then this one.
- `OnExit` already unregistered the Playback Hotkey (ticket 01). I only dropped its "not registered yet" comment.

**Console and tray**

- Header: `ConsoleSnippingTool`, `Press Ctrl+Win+Z to capture`, `Hold Ctrl+Win+X to dictate`, `Press Ctrl+Win+A to transcribe playback, again to send`, `Esc or right-click cancels a capture; Esc discards a microphone dictation`, then the blank line.
- Tooltip: `ConsoleSnippingTool (capture Ctrl+Win+Z, dictate Ctrl+Win+X, playback Ctrl+Win+A)`.

**Shared builder**

- `ConfigJson.With` has two new parameters after `listeningTimeoutSeconds`:
  - `playbackHotkey`: plain text, default `Ctrl+Win+A`;
  - `playbackTimeoutSeconds`: a JSON literal, default `"600"`.
- Ticket 03 can set the Playback cap with `ConfigJson.With(playbackTimeoutSeconds: "30")`.

**Deviations**

1. I added a test that the tooltip fits the tray's 127 characters with three `Ctrl+Alt+Shift+Win+F2x` Hotkeys (117 characters). The spec states this limit; the acceptance list does not.
2. The builder always writes every field. So the tests with a missing field (`playbackHotkey` omitted, `dictation.hotkey` omitted) use handwritten one-line JSON, as the existing not-an-object tests do.
3. I fixed `Config.cs` doc comments. `DictationHotkey` and `ListeningTimeoutSeconds` now say Microphone. `Insert` says it applies to the Microphone only and that Playback always Copies. The behaviour is ticket 03's.
4. The Tray example in `FakePlatform.cs`'s header comment (`SetTrayTooltip(ConsoleSnippingTool (Ctrl+Win+Z))`) was already stale. I left it, because that file is not in this ticket's list.
