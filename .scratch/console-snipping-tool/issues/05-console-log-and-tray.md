# 05 Console, log ring and tray state

Status: resolved
Blocked by: 02

Spec: `.scratch/console-snipping-tool/spec.md` → Console and log, Tray, user stories 30–35. ADR-0001.

## Goal

Show/Hide, the in-memory log ring and the tray menu state, in `SnipperCore.Console.cs`, a new `LogRing` type, the `Log` method in `SnipperCore.cs`, and a new test file `ConsoleTests.cs`. Do not touch `IPlatform`, Startup or Capture files.

## Behaviour

- `LogRing(capacity 200)`: appends lines, drops the oldest beyond capacity, enumerates oldest first.
- `Log(line)`: prefixes a local timestamp `HH:mm:ss`, appends to the ring, and calls `WriteConsoleLine` only while the Console is shown. Timestamps come from an injectable clock (`Func<DateTime>` or `TimeProvider`) defaulting to local time so tests can pin them.
- `OnShow()` when hidden: `ShowConsole()`, then `WriteConsoleLine` for the header (tool name, "Press <Hotkey> to capture", "Esc or right-click cancels"), a blank line, then every ring line in order; `SetTrayMenuState(showEnabled: false, hideEnabled: true)`. `OnShow` while shown does nothing.
- `OnHide()` when shown: `HideConsole()`, `SetTrayMenuState(true, false)`. `OnHide` while hidden does nothing.
- `OnExit` while shown hides the Console first (it is freed with the process anyway; keep the call so the adapter can detach its control handler).

## Tests (ConsoleTests.cs)

Show prints header then ring; lines logged while hidden appear on the next Show; lines logged while shown are written immediately and once; Hide then Show replays; ring caps at 200 (log 250, the first 50 are gone); repeated Show or Hide is idempotent; menu state follows visibility.

## Acceptance

`dotnet test` green; `IPlatform` unchanged.

## Comments
- Resolved in eae1f15 (05: console, log ring and tray state; branch tip 0558d7e merges 03 and 04), merged onto integration/console-snipping-tool; build green, 68 tests green on integration. For ticket 08: constructor is now SnipperCore(IPlatform, Func<DateTime>? clock = null); Log is private, so an unhandled-exception entry point must be added in a core file; Show prints the header lines "ConsoleSnippingTool", "Press <Hotkey> to capture", "Esc or right-click cancels", "" then the ring; SetTrayMenuState is called once more right before RemoveTrayIcon on exit.
