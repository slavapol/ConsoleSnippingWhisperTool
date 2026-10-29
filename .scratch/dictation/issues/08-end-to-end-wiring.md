# 08 End-to-end wiring, publish and smoke run

Status: ready-for-agent
Blocked by: 03, 04, 05, 06, 07

Spec: `.scratch/dictation/spec.md` → everything; this ticket makes the exe do what the spec says. Prior art: `.scratch/console-snipping-tool/issues/08-end-to-end-wiring.md`.

## Goal

Every stub ticket 01 left has been replaced (search the adapter for `not implemented`), the transport uses the shared `Wav` writer, the adapter and the core agree on every message, and the published exe is exercised. Any mismatch between tickets is resolved here and recorded.

## Scope

- Remove every leftover stub or local duplicate (for example a WAV header copy in the transport if ticket 06 merged before 05).
- Confirm `Program.cs` needs no change; if the message window's posted-action queue or the Dictation Hotkey id needs wiring that no ticket did, do it here.
- Reconcile the spec: where the built behaviour deviates from `.scratch/dictation/spec.md`, either fix the code or change the spec and say which in Comments. Update `GLOSSARY.md` or the ADRs only if a term or decision actually changed.
- `dotnet build`, `dotnet test`, `dotnet publish -c Release src/ConsoleSnippingTool` (single-file, framework-dependent) all succeed; the exe size is recorded.
- Smoke run without the desktop, in the published exe's folder with no `config.json`: the process is alive after 2 s; `config.json` was created with exactly `new Config().ToJson()`; `RegisterHotKey` probes for `Ctrl+Win+Z` and `Ctrl+Win+X` fail with 1409 while it runs; a second copy replaces the first; after stopping it both probes succeed. Delete the created `config.json` afterwards.

## Manual checklist (for the user; a subagent without a desktop says so and leaves these)

1. Set `OPENAI_API_KEY`, launch: tray tooltip shows both Hotkeys; the Console header shows both and the discard rule.
2. Hold `Ctrl+Win+X`, say a sentence, release: click, click, then the text appears in Notepad with a third click; the Console shows Listening, Sent, Inserted with the wait and the text.
3. Dictate into a browser text box, into Windows Terminal, and into a chat app with two sentences separated by a pause; nothing is sent prematurely.
4. Take a Capture, then dictate: the image is still pasteable afterwards; `Win+V` history does not contain the Transcript.
5. Tap the chord: nothing happens except `Tap ignored` in the Console. Hold, press Esc: `Discarded (Esc)`. Hold for over two minutes: the cap transcribes.
6. Hold `Ctrl+Win+X` while Armed, and `Ctrl+Win+Z` while Listening: both ignored with a Console line.
7. Remove the key: balloon at startup and on each press. Unplug or disable the microphone: `No microphone found`. Disconnect the network: `Dictation failed: ...`.
8. Set `dictation.cleanup` to true: the text comes back cleaned; the Console shows the extra wait.
9. Dictate into an elevated console: nothing appears (documented limitation).

## Acceptance

Build, tests and publish green; the smoke results and any reconciliation are in Comments with honest outcomes; every earlier ticket is `resolved`.

## Comments
