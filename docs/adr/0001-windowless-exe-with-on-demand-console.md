# Windowless exe with an on-demand console

The tool is named "Console…" and its only visible surface is a console window, yet the project is `OutputType=WinExe`, not a console project. The tray icon, global hotkey and clipboard all need a Windows message loop on an STA thread, which a console `Main` doesn't have, and a console project flashes a black window on every launch (and on every VS F5) before it could be hidden. So the process starts with no window at all, runs a message loop for the tray and hotkey, and `AllocConsole`s a real console only when the user picks Show, freeing it on Hide.

## Consequences

- Closing an allocated console with its X button kills the owning process, so the X is disabled; the console is closed from the tray.
- Ctrl+C inside the console would also terminate the process and is ignored.
- The console's buffer is lost on Hide, so the log is kept in memory and replayed on Show.
