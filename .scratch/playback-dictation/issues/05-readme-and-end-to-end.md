# 05 README and end-to-end

Status: resolved
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

### Implemented (2026-10-30)

`dotnet build` 0 warnings; `dotnet test` 256 green, none added or deleted. `dotnet publish -c Release src/ConsoleSnippingTool` gives `ConsoleSnippingTool.exe`, 294,633 bytes (288 KB; the Dictation exe was 270 KB), beside only its `.pdb`.

**Stubs.** None left. A search of `src` and `tests` for `not implemented`, `NotImplemented`, `TODO`, `FIXME`, `stub`, `placeholder` and `until ticket` finds nothing from tickets 01 to 04. `IPlatform` and `Program.cs` are unchanged; `MessageWindow` already sends `PlaybackHotkeyId` to `OnPlaybackHotkey()`.

**Comments only, no behaviour change, so no tests.**

- `FakePlatform.cs` header: the tooltip example ticket 02 left is now `SetTrayTooltip(ConsoleSnippingTool (capture Ctrl+Win+Z, dictate Ctrl+Win+X, playback Ctrl+Win+A))`, and the request examples say `timeout:21s`, which is what the fake's 3 s clip gives.
- Five production comments that still said microphone or 20 s: `_listeningSince`, `OnExit`, `Wav`, `IPlatform.RegisterCancelKey` and `OpenAiHttp`.

**README**

- Config reference: the two fields, `insert` for the Microphone only, which settings are shared, the new default file, three hotkeys in the grammar, and the Ready and Sent sounds for both Sources.
- A Playback Dictation section (press, press again, Tap, the cap, Copy only, Esc left alone, what is recorded, which device, one job at a time, the longer timeout), the consent note, and a "Playback hears nothing" troubleshooting entry.
- The five full examples gained the two fields. Without them each would have loaded with two "is missing" balloons.
- Checked with a throwaway pwsh run of `Config.Load` on every README JSON block: the default file and the five examples give no problems, the default block equals `new Config().ToJson()`, and the mistake example gives exactly the 14 lines shown.

**Smoke run** (published exe, no `config.json`, `OPENAI_API_KEY` removed from the launching environment, no input sent; probe `RegisterHotKey(NULL, 1, MOD_CONTROL|MOD_WIN, vk)` for Z, X and A). Before the launch all three probes were free.

- (a) PASS: alive 2 s after launch.
- (b) PASS: `config.json` created, 527 bytes, CRLF, no BOM, equal to `new Config().ToJson()`.
- (c) PASS: `Ctrl+Win+Z`, `Ctrl+Win+X` and `Ctrl+Win+A` all fail with 1409 while it runs.
- (d) PASS: a second copy replaced the first. The first exited within 5 s with code 0, all three probes still gave 1409, and `config.json` was unchanged.
- (e) PASS: setting `Local\ConsoleSnippingTool.Replace` stopped the second copy within 5 s with code 0, and all three probes then succeeded. `config.json` was deleted and no process was left.
- Not observed: the tray, the missing-key balloon and the Console.

**Spec reconciliation.** None needed. The built behaviour matches the spec as tickets 03 and 04 left it: the 1 s nothing-played threshold, `ceil(20 + seconds/6)`, every log line, call order and message. The deviations recorded in tickets 01 to 04 are internal (the capture's `DeviceName` property, `[PreserveSig]`, test knobs) or were already folded into the spec. `GLOSSARY.md` and the ADRs are unchanged.

**Manual checklist.** Items 1 to 10 not run: they need a meeting and a live API key, so they are left for the user. For item 1, `ConsoleTests` pins the tooltip and header text, and (c) shows all three Hotkeys registered. Ticket 04's harness already confirmed `LOOPBACK | AUTOCONVERTPCM` at 16 kHz mono on this PC.

- Resolved: branch tip 02bce2c fast-forwarded onto integration/playback-dictation; build 0 warnings, 256 tests green on integration. The manual checklist (items 1 to 10) is left for the user.
