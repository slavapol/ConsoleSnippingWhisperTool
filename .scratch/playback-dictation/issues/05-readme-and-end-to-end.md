# 05 README and end-to-end

Status: ready-for-agent
Blocked by: 02, 03, 04

Spec: `.scratch/playback-dictation/spec.md` → everything, README in particular. Prior art: `.scratch/dictation/issues/08-end-to-end-wiring.md`.

## Goal

The exe does what the spec says, the README documents it, and the published build is smoke-tested.

## Scope

- Search the code for anything tickets 01 to 04 left as a placeholder or stub and finish it. Reconcile the spec: where the built behaviour deviates, fix the code or change the spec and say which in Comments; update `GLOSSARY.md` or the ADRs only if a term or decision actually changed.
- README, per the spec's README section: the two fields in the config reference and the default file; `insert` applies to the Microphone only; a short section on using a Playback Dictation; the consent note; the troubleshooting entry for Playback hearing nothing.
- `dotnet build`, `dotnet test`, `dotnet publish -c Release src/ConsoleSnippingTool` succeed; record the exe size.
- Smoke run as in the prior-art ticket, now also probing `Ctrl+Win+A`: while the exe runs, `RegisterHotKey` probes for `Ctrl+Win+Z`, `Ctrl+Win+X` and `Ctrl+Win+A` fail with 1409, and all succeed after it stops. Delete the created `config.json` afterwards.

## Manual checklist (for the user; a subagent without a desktop says so and leaves these)

1. Launch with a key set: the tooltip and the Console header show all three Hotkeys.
2. Join a Teams or Zoom test call on laptop speakers; press `Ctrl+Win+A`, let the other side talk, press again: Ready, Sent, Done; the Transcript is on the clipboard; the Listening line names the speakers.
3. Repeat with a Bluetooth headset in the call; if nothing is heard, the Listening line names a different device than the meeting app uses.
4. Play a video in a browser and transcribe it.
5. Press `Ctrl+Win+A` twice with nothing playing: `Nothing heard (nothing played)`, no Sent.
6. During Listening press Esc in a full-screen Zoom: Zoom leaves full screen and Listening goes on.
7. During Listening press `Ctrl+Win+Z` and `Ctrl+Win+X`: both ignored with a Console line.
8. Set `playbackTimeoutSeconds` to 20 and let the cap end a Playback Dictation: Sent with ` (cap)`.
9. With `dictation.insert` true, a Playback Transcript is still only copied.
10. Hold `Ctrl+Win+X` and dictate: the Listening line names the microphone.

## Comments
