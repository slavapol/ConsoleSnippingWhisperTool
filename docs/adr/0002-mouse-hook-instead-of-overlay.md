# Corners are picked with a mouse hook, not an overlay window

Every region-capture tool, including Windows Snipping Tool, puts a transparent full-screen overlay on top of the desktop so the user's clicks land on it and it can dim the screen, draw a crosshair and preview the rectangle. This tool is required to show no user elements while working, so instead a low-level mouse hook (`WH_MOUSE_LL`) is installed only while Armed: it reads the two left-button presses as Corners and swallows those down/up pairs so they never reach the window underneath. The hook is removed as soon as the Capture ends or is cancelled.

## Consequences

- There is no visual feedback at all while Armed; sounds are the only signals. Changing the cursor would need `SetSystemCursor`, a global hack, so it isn't done.
- Esc while Armed is caught by registering it as a temporary hotkey, which steals Esc from other apps for those seconds; consistent with the clicks already being swallowed.
- An Armed timeout exists because a forgotten hotkey press would otherwise swallow clicks indefinitely.
