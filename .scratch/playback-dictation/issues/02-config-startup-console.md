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
