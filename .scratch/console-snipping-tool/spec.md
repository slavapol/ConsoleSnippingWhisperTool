# ConsoleSnippingTool

Status: ready-for-agent

## Problem Statement

Taking a screenshot of part of the screen on Windows means opening the Snipping Tool, which dims the screen, draws an overlay, and ends in an editor window that then has to be closed. For someone who just wants a rectangle on the clipboard many times a day, every one of those elements is friction. The user wants a tool that is invisible until needed, needs nothing but a hotkey and two clicks, and confirms what it did with sound alone.

## Solution

A tray-resident Windows tool. It starts with no window, sits in the tray, and waits for a Hotkey (`Ctrl+Win+Z` by default). The Hotkey arms it with a Ready signal; the user clicks two opposite Corners; the Region between them lands on the clipboard and a Done signal plays. Nothing is drawn on screen at any point. A tray menu offers Show, Hide and Exit, where Show opens a Console that displays the Hotkey and a log of Captures. The Hotkey and the sounds are configurable in a JSON file beside the exe. Starting the tool a second time replaces the running Instance.

## User Stories

### Starting and stopping

1. As a user, I want the tool to start with no window, so that launching it never interrupts what I'm doing.
2. As a user, I want a tray icon to appear as soon as the tool is running, so that I can tell it is active.
3. As a user, I want the tray icon's tooltip to show the current Hotkey, so that I can find out how to capture without opening anything.
4. As a user, I want a tray menu with exactly Show, Hide and Exit, so that the tool's whole interface fits in one right-click.
5. As a user, I want Exit to remove the tray icon and release the Hotkey, so that nothing of the tool lingers after it closes.
6. As a user, I want Exit while Armed to cancel the Capture first, so that my clicks are never left swallowed by a dead process.
7. As a user, I want starting the exe a second time to close the previous Instance and take over, so that relaunching after a config edit "just works".
8. As a user, I want a hung previous Instance to be terminated after a short wait, so that a stuck process can't block the new one.

### Capturing

9. As a user, I want to press `Ctrl+Win+Z` from any app and have the tool become Armed, so that capturing never requires switching windows.
10. As a user, I want to hear a Ready signal when the tool becomes Armed, so that I know my clicks are now being read.
11. As a user, I want my first left click to register a Corner and play a Corner signal, so that I know it was taken.
12. As a user, I want my second left click to complete the Region immediately, so that the capture takes exactly two clicks.
13. As a user, I want the two clicks to be swallowed, so that clicking a Corner over a link or button never activates it.
14. As a user, I want everything other than those two left clicks (mouse movement, other apps' keys) to keep working while Armed, so that arming the tool doesn't freeze my machine.
15. As a user, I want to click the Corners in any order (top-left then bottom-right, or top-right then bottom-left), so that I don't have to think about direction.
16. As a user, I want the Region to include both Corner pixels, so that what I clicked on is in the image.
17. As a user, I want Corners on different monitors to work, so that a Region can span screens.
18. As a user, I want the image at native pixel resolution on scaled (high-DPI) monitors, so that captures are never blurry.
19. As a user, I want the mouse cursor left out of the image, so that the capture shows only the screen content.
20. As a user, I want the Region put on the clipboard as both PNG and bitmap, so that it pastes into Paint, Word, browsers and chat apps alike.
21. As a user, I want a Done signal when the image is on the clipboard, so that I know I can paste.
22. As a user, I want the clipboard contents to survive the tool exiting, so that I can paste later.
23. As a user, I want nothing drawn on screen at any point of a Capture (no overlay, no dimming, no crosshair, no balloon), so that the tool stays invisible.

### Cancelling

24. As a user, I want Esc to cancel an Armed Capture, so that a mistaken Hotkey press is harmless.
25. As a user, I want a right-click to cancel an Armed Capture, so that I can cancel without reaching for the keyboard.
26. As a user, I want pressing the Hotkey again while Armed to cancel, so that the Hotkey toggles rather than stacks.
27. As a user, I want an Armed Capture to cancel itself after 30 seconds without a Corner, so that a forgotten Hotkey press never leaves my clicks swallowed.
28. As a user, I want the timeout to restart after the first Corner, so that I have the full time to pick the second one.
29. As a user, I want cancel to be silent and copy nothing, so that a cancel is never confused with a Capture.

### Console

30. As a user, I want Show to open a Console that prints the current Hotkey and how to cancel, so that I can check the shortcut.
31. As a user, I want the Console to print one timestamped line per event (Armed, Corner, Captured with size, Cancelled, Timed out, errors), so that I can see what the tool did.
32. As a user, I want Hide then Show to bring the recent log back, so that hiding the Console doesn't lose history.
33. As a user, I want double-clicking the tray icon to do the same as Show, so that the Console is one click away.
34. As a user, I want Show greyed out while the Console is open and Hide greyed out while it is closed, so that the menu reflects the state.
35. As a user, I want the Console's close button and Ctrl+C to be unable to kill the tool, so that an accidental close doesn't exit it (under Windows Terminal the close button exits the tool cleanly instead; see ADR-0001).

### Configuration

36. As a user, I want a `config.json` beside the exe, so that settings travel with the tool.
37. As a user, I want the file created with defaults if it's missing, so that I can find and edit it.
38. As a user, I want to set the Hotkey as a string like `Ctrl+Shift+F5`, so that I can pick a combination that doesn't clash with my other tools.
39. As a user, I want to set the Armed timeout in seconds, so that I can make it shorter or longer.
40. As a user, I want to point each of the Ready, Corner and Done signals at my own `.wav`, so that I can choose the sounds.
41. As a user, I want to silence any individual signal, so that I can, for example, keep only the Done signal.
42. As a user, I want a missing or unreadable `.wav` to fall back to the built-in click with a warning in the Console, so that a bad path never makes the tool silent by surprise.
43. As a user, I want an invalid field to fall back to its default with a balloon and a Console line naming it, so that a typo in the config never stops the tool from starting.
44. As a user, I want a Hotkey already taken by another app to be reported with a balloon and a Console line while the tool keeps running, so that I can fix the config and relaunch.
45. As a user, I want config edits to apply on the next launch, so that the rule is simple: edit, relaunch.

### Sounds

46. As a user, I want the default sound to be a short keyboard-like click, so that the signals are quick and unobtrusive.
47. As a user, I want sounds played without delaying the Capture, so that the second click and the clipboard write are never held up.
48. As a user, I want a clipboard failure to produce no Done signal plus a balloon and a Console line, so that I'm not misled into pasting nothing.

### Developer

49. As a developer, I want the project to open and run from Visual Studio 2026, so that I can debug it there.
50. As a developer, I want `dotnet publish` to produce a single-file exe, so that the tool is easy to put in a folder and run.
51. As a developer, I want the core logic tested without touching Windows, so that the tests run anywhere and fast.

## Implementation Decisions

### Process shape

- A `WinExe` targeting `net10.0-windows`, no window at launch. One STA thread runs a Windows message loop that owns the tray icon and receives hotkey messages. See ADR-0001.
- WinForms is referenced as a library only (tray icon, clipboard, screen metrics, screen-to-bitmap copy); no Form is ever shown.
- The Console is a real console window allocated on Show and freed on Hide. Its close button is removed from the system menu and the console control handler ignores Ctrl+C and close events. Under Windows Terminal the close event cannot be vetoed, so closing the console there exits the tool cleanly (Hotkey released, tray icon removed). See ADR-0001.
- Per-Monitor-V2 DPI awareness is declared in the application manifest so hook coordinates and screen copies are in physical pixels on the virtual screen.
- No third-party runtime packages: Win32 is reached through a small hand-written P/Invoke set (hotkey registration, low-level mouse hook, console allocation and control handler, system menu, named mutex/event, process termination); JSON through the BCL.
- An exception nothing else caught on the message loop is reported with a balloon and a Console line, and the tool keeps running.

### Core and Platform seam

- The application is split into a **core** and a **Platform**. The Platform is a single interface for everything the OS does: register and release a hotkey, install and remove the mouse hook, start and stop the Armed timer, copy a Region of the screen to an image, put an image on the clipboard, play a sound, show and hide the Console, write a Console line, set the tray tooltip and menu enabled states, show a balloon, and the single-instance handshake (try acquire, signal previous, wait for exit, terminate previous). The Win32 adapter implements it.
- The core is a state machine with states **Idle**, **Armed** and **FirstCorner** (Armed with one Corner taken). It receives events (hotkey pressed, left-button down at x,y, right-button down, Esc, timer elapsed, Show, Hide, Exit, config text loaded) and responds only by calling the Platform. All logic lives in the core; the adapter contains no decisions.
- Transitions:
  - Idle + hotkey → start timer, register Esc, install hook, play Ready, Armed (the hook last, so nothing that could fail runs while clicks are already being swallowed).
  - Armed + left down → record Corner, restart timer, play Corner, FirstCorner.
  - FirstCorner + left down → compute Region, remove hook, release Esc, stop timer, copy Region, set clipboard, play Done, Idle.
  - Armed/FirstCorner + Esc, right down, hotkey or timer → remove hook, release Esc, stop timer, Idle, no sound.
  - Any state + Exit → cancel if Armed, release hotkey, remove tray icon, stop.
- While installed, the hook swallows every left and right button down with its matching up and posts the down to the message loop for the core, because Windows waits only a bounded time for a low-level hook and the second-corner Capture can exceed it; since the hook exists only in Armed and FirstCorner, that equals swallowing exactly the clicks the core consumes, and the core's remove-hook call is deferred until a pending button-up has been swallowed. Everything else passes through. See ADR-0002 for the mechanism and its two residual races.

### Region

- A Corner is the left-button *down* position in virtual-screen coordinates. The Region is the inclusive bounding rectangle of the two Corners: width = |x₂−x₁|+1, height = |y₂−y₁|+1, so the minimum Region is 1×1 and two clicks on one spot produce a 1×1 image rather than a cancel.
- The clipboard receives both a PNG stream and a device-independent bitmap, with the data kept after the process exits. The write is retried for roughly half a second before being reported as failed.

### Hotkey

- The Hotkey string is one or more of `Ctrl`, `Alt`, `Shift`, `Win` joined by `+` with exactly one key: a letter, a digit or `F1`–`F24`. `Control` and `Windows` are accepted as aliases of `Ctrl` and `Win`. Parsing is case-insensitive and tolerant of surrounding whitespace. A string with no modifiers or no key is invalid.
- Esc is registered as a second hotkey only while Armed and released on leaving Armed. See ADR-0002.
- A failed Hotkey registration is reported with a balloon and a Console line; the tool keeps running without a Hotkey.

### Configuration

- A JSON file `config.json` in the exe's folder, read once at startup. If absent, written with defaults. Keys are camelCase.
- Fields: `hotkey` (string, default `Ctrl+Win+Z`); `armedTimeoutSeconds` (integer from 1 to 3600, default 30); `sounds` with `ready`, `corner`, `done`, each `null` (built-in click), the string `none` (silent) or a `.wav` path, relative paths resolved from the exe folder.
- The parser tolerates JSON comments and trailing commas, since the file is hand-edited.
- Any field that is missing, of the wrong type or invalid falls back to its default; each such field is reported once with a balloon and a Console line. Unparseable JSON means all defaults with one report. A config file that cannot be written (read-only exe folder) is reported the same way and startup continues with defaults. The tool never refuses to start over configuration.

### Sounds

- The built-in click is synthesised in memory at startup: a burst of roughly 30 ms of noise with a fast exponential decay, written as a 16-bit mono PCM WAV stream, so no audio asset ships.
- All three signals use the built-in click unless configured. Playback is asynchronous. A configured file that is missing or fails to load falls back to the built-in click with a Console warning at startup.

### Console and log

- The log is an in-memory ring of the last 200 lines, each prefixed with a local timestamp. Show prints the header (tool name, Hotkey, cancel keys) and then the ring; while shown, new lines are printed as they occur.
- Events logged: Armed, Corner 1 with coordinates, Captured with width×height, Cancelled with reason (Esc, right-click, hotkey, timeout), clipboard failure, configuration fallbacks, Hotkey registration failure, Instance replacement.

### Tray

- The icon is drawn in code at startup (a simple rectangle glyph); no `.ico` asset. The icon does not change while Armed. No balloon at startup or after a Capture.
- Tooltip: tool name and current Hotkey. Menu: Show, Hide, Exit; Show enabled only while the Console is hidden, Hide only while shown. Double-click equals Show.

### Single instance

- A named mutex identifies the running Instance and a named event asks it to exit. The new Instance, finding the mutex taken, signals the event and waits up to 5 s for the mutex to be released (the old Instance cancels any Capture, releases the Hotkey, removes its tray icon and exits). If the wait expires, the new Instance terminates the old process and continues. Both names are scoped to the current session.

### Repository

- A solution at the root with the app project under `src` and the test project under `tests`. Nullable reference types and implicit usings enabled.
- `dotnet publish -c Release` produces a single-file, framework-dependent `win-x64` exe.
- Git repository initialised with a .NET `.gitignore`.

## Testing Decisions

- A good test drives the core through its public events and asserts only on the calls made to the Platform and their order. It never inspects the core's state fields and never touches Windows.
- One seam: a recording fake Platform. The test project (xUnit v3) covers the state machine, Region arithmetic (order independence, inclusivity, 1×1), hotkey parsing (valid combinations, case, whitespace, missing key, missing modifier, bad key), configuration loading (defaults, missing file, bad JSON, each invalid field falling back independently, sound values), cancel paths (Esc, right-click, hotkey, timeout, timeout restart after first Corner), clipboard failure (no Done signal, balloon), the log ring (cap at 200, replay on Show), and the single-instance handshake (acquire, signal and wait, terminate after timeout).
- The Win32 adapter, the console window and the tray icon are not tested; they are kept free of logic so that there is nothing in them to test.
- There is no prior art in the repository; this is the first code.

## Out of Scope

- Running at Windows startup.
- Saving captures to disk, keeping a history, or any image editing or annotation.
- Delayed capture, window capture, full-screen capture, or a press-drag-release gesture.
- Live config reload or a Reload menu item.
- Any visual feedback while Armed (cursor change, overlay, dimming, tray icon change).
- A separate cancel sound.
- Installer, code signing, auto-update.

## Further Notes

- Glossary terms are in `GLOSSARY.md`; the two architectural deviations from the usual way of building a region-capture tool are recorded in `docs/adr/0001` and `docs/adr/0002`.
- `Win+Z` alone is taken by Windows 11 snap layouts; `Ctrl+Win+Z` is free on a default install, which is why it is the default.
- The machine this was specified on has .NET SDK 10.0.301, the .NET 10 Windows Desktop runtime and Visual Studio Enterprise 2026 (18.6), so the stated targets build without extra installs.
