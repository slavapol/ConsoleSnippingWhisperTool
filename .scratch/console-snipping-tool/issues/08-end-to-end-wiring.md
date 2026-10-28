# 08 End-to-end wiring, publish and manual run

Status: ready-for-agent
Blocked by: 03, 04, 05, 06, 07

Spec: `.scratch/console-snipping-tool/spec.md` → everything; this ticket makes the exe do what the spec says.

## Goal

`Program.cs` composes `Win32Platform` and `SnipperCore` on the STA thread, calls `Start()`, and runs the message loop. Any mismatch that the earlier tickets left between the adapter and the core (for example the built-in click placeholder in ticket 07, or an `IPlatform` member nobody calls) is resolved here. Then the published exe is exercised by hand.

## Scope

- `Program.cs`: `[STAThread] Main` → `ApplicationConfiguration.Initialize()` if the project uses it, create `Win32Platform`, create `SnipperCore`, `platform.Attach(core)` (or constructor order, whatever ticket 07 chose), `core.Start()`, `Application.Run()` with no form. Unhandled exceptions are logged through the core's log and shown as a balloon rather than crashing silently.
- Make sure `ClickSound.Wav()` is what `BuiltIn` plays.
- `dotnet publish -c Release` and run the published exe for the manual checklist below. Record the outcome of each item in this ticket's Comments; do not mark anything as checked that was not actually observed. A subagent that cannot interact with the desktop must say so and leave the items for the user.

## Manual checklist

1. Launch: no window, tray icon appears, tooltip shows the Hotkey, `config.json` is created beside the exe with defaults.
2. Hotkey → click sound; two clicks on different windows → click sounds; paste into Paint and into a browser text box: both get the image; nothing was activated under the clicks.
3. Corners on two monitors (if available) and on a scaled monitor: image is at physical resolution.
4. Esc, right-click, Hotkey again, and waiting 30 s each cancel silently; afterwards clicks reach apps normally.
5. Show: console with header and log; Hide; Show again replays the log; the console's X is disabled; Ctrl+C in it does nothing.
6. Edit `config.json` to a bad hotkey and relaunch: balloon, still in tray. Set the hotkey to one taken by another app: balloon, still running.
7. Launch a second copy: the first disappears from the tray, the second works.
8. Exit removes the icon; the pasted image is still on the clipboard.

## Acceptance

`dotnet build`, `dotnet test`, `dotnet publish -c Release` all succeed; the checklist is in Comments with honest results.

## Comments
- Implemented on branch `worktree-agent-a99ff1813ab2775fb` (commits `08: ...`). `dotnet build` 0 warnings; `dotnet test` 80 green (78 + 2 new StartupTests: an unhandled exception is shown as a balloon and logged; a config.json that cannot be written is reported and startup continues with defaults, via a `WriteConfigThrows` knob on FakePlatform); `dotnet publish -c Release src/ConsoleSnippingTool` → single-file framework-dependent `ConsoleSnippingTool.exe` (214 KB) in `src/ConsoleSnippingTool/bin/Release/net10.0-windows/win-x64/publish/`. `IPlatform` unchanged.
- Program.cs: `SetUnhandledExceptionMode(CatchException)`, `EnableVisualStyles()`, `SetCompatibleTextRenderingDefault(false)` → `new Win32Platform()` → `new SnipperCore(platform)` → `Attach` → `Start()` (a failure there is reported and the loop still runs, so the tray's Exit works) → `Application.Run()`. `ApplicationConfiguration.Initialize()` is not called: its generated body (checked with `EmitCompilerGeneratedFiles`) also calls `SetHighDpiMode(SystemAware)`, against the manifest's Per-Monitor-V2 that the WFO0003 suppression records. `SnipperCore.OnUnhandledException` (balloon + log `Unhandled error: <type>: <message>`) receives `Application.ThreadException` and `AppDomain.UnhandledException`.
- Ticket 07's open items: the mouse hook callback no longer calls the core. It swallows every left/right button down with its matching up and posts `WM_LEFT_BUTTON_DOWN(x, y)` / `WM_RIGHT_BUTTON_DOWN` to the message window, so the core runs on the loop after the callback returned (the equivalence with the core's consume answer is in the callback comment; `InstallMouseHook` always hooks afresh so a hook Windows dropped after a timeout cannot leave a stale handle). Residual: if a very large capture takes longer than the low-level hook timeout, the second click's button-up alone may reach the window under the cursor (no preceding down, so benign). The `LLMHF_INJECTED` filter is removed. `CTRL_CLOSE_EVENT` posts `WM_EXIT_REQUESTED` and waits up to 3 s for `Exit()` before returning TRUE (ADR-0001 Consequences updated); Ctrl+C and Ctrl+Break are still ignored. A `WriteConfigText` failure → balloon + log `config.json could not be written: <message>; continuing with defaults`.
- Smoke run, 2026-10-28, non-interactive only (published exe started from PowerShell; no keyboard or mouse input was sent; the probe is `RegisterHotKey(NULL, 1, MOD_CONTROL|MOD_WIN, 0x5A)` via Add-Type, which returned 0 = free before the launch):
  - (a) PASS: the process was alive 2 s after launch (pid 38684).
  - (b) PASS: `config.json` was created beside the exe with exactly the default content (deleted again afterwards so checklist item 1 can be observed fresh).
  - (c) PASS: the probe failed with Win32 error 1409 (ERROR_HOTKEY_ALREADY_REGISTERED) while the tool ran.
  - (d) PASS: a second copy was started; the first exited within 5 s with exit code 0; the second was alive and the probe still failed with 1409.
  - (e) PASS: after `Stop-Process` of the second copy the probe succeeded (0) and was unregistered again. A killed process's tray icon may linger until hovered; Windows behaviour, not a bug.
- Manual checklist: items 1–8 not run: needs the user's desktop. Partial automated coverage: item 1's `config.json` creation is (b) above; item 7's process half (the first Instance exits, the second owns the Hotkey) is (d) above; whether the tray icon appears, its tooltip, sounds, clicks, clipboard, Console, cancels and Exit are the user's to observe.
