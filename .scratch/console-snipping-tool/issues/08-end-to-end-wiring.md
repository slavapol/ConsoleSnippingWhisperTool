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
