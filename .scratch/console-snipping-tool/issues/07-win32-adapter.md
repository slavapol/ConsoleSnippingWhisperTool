# 07 Win32 Platform adapter

Status: resolved
Blocked by: 02

Spec: `.scratch/console-snipping-tool/spec.md` → Process shape, Core and Platform seam, Region, Single instance, Tray, Console. ADRs 0001 and 0002.

## Goal

`Win32Platform : IPlatform` in `src/ConsoleSnippingTool/Win32/`, implementing every member of the frozen interface against real Windows, with no logic beyond what each call says. It is not unit-tested (Testing Decisions); the acceptance test is building, plus a manual run in ticket 08. Do not touch `IPlatform` or the Core folder; do not write `Program.cs` beyond what ticket 01 left (ticket 08 wires things up). The adapter is constructed with the `SnipperCore` it must call back into; the message loop thread is the STA thread that creates it.

## Pieces

- **Message window**: a hidden native window (a `NativeWindow` subclass is fine) owned by the STA thread that receives `WM_HOTKEY` and forwards: the capture Hotkey id → `core.OnHotkey()`, the Esc id → `core.OnCancelKey()`. `RegisterHotkey`/`UnregisterHotkey`/`RegisterCancelKey`/`UnregisterCancelKey` use `RegisterHotKey`/`UnregisterHotKey` with `MOD_NOREPEAT`. Esc is `VK_ESCAPE` with no modifiers.
- **Mouse hook**: `SetWindowsHookEx(WH_MOUSE_LL)` installed/removed on the STA thread, delegate kept in a field so it isn't collected. On `WM_LBUTTONDOWN` call `core.OnLeftButtonDown(pt.x, pt.y)`; if true, return 1 (swallow) and remember to swallow the next `WM_LBUTTONUP`. On `WM_RBUTTONDOWN` call `core.OnRightButtonDown()`; if true swallow it and the next `WM_RBUTTONUP`. Everything else `CallNextHookEx`. Ignore injected events (`LLMHF_INJECTED`). Keep the hook callback fast: no logging, no allocation beyond the call.
- **Armed timer**: a `System.Windows.Forms.Timer` (runs on the message loop) calling `core.OnTimerElapsed()`; `Start` restarts it.
- **Capture**: `TryCaptureToClipboard(region)`: `new Bitmap(w, h)`, `Graphics.CopyFromScreen(left, top, 0, 0, size, CopyPixelOperation.SourceCopy)`, then a `DataObject` with `DataFormats.Bitmap` (the bitmap) and a `MemoryStream` of the PNG under the `"PNG"` format, set via `Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 50)`; catch `ExternalException` → false. Dispose the bitmap after the copy. Physical pixels are guaranteed by the manifest from ticket 01.
- **Sounds**: `TrySetSound` builds a `SoundPlayer` per signal: for `BuiltIn` from a `MemoryStream` of `ClickSound.Wav()` (ticket 06; if it isn't merged yet, use a 30 ms silent WAV placeholder and leave a comment to swap); for `File` load from the path resolved against the exe folder and return false on any exception; for `None` store null. `PlaySound` calls `Play()` (asynchronous) when a player exists.
- **Console**: `ShowConsole` → `AllocConsole()`, then `GetConsoleWindow()`, `GetSystemMenu(hwnd, false)` + `DeleteMenu(SC_CLOSE, MF_BYCOMMAND)`, `SetConsoleCtrlHandler(handler, true)` whose handler returns true for every event (Ctrl+C, Ctrl+Break, close) so nothing terminates the process, and re-open `Console.Out` on the new console (`Console.SetOut` with a `StreamWriter` over `Console.OpenStandardOutput()`, autoflush). `HideConsole` → remove the handler, `FreeConsole()`. `WriteConsoleLine` → `Console.Out.WriteLine` (no-op when not allocated).
- **Tray**: a `NotifyIcon` with an icon drawn in code (a 16×16 and 32×32 bitmap with a 2-px rectangle outline and small corner ticks, converted via `Icon.FromHandle` of `GetHicon`, handle kept alive), a `ContextMenuStrip` with Show / Hide / Exit calling `core.OnShow/OnHide/OnExit`, `DoubleClick` → `core.OnShow()`. `SetTrayTooltip` → `Text` (max 63 chars). `SetTrayMenuState` toggles `Enabled`. `ShowBalloon` → `ShowBalloonTip(3000, title, text, ToolTipIcon.Warning)`. `RemoveTrayIcon` → `Visible = false; Dispose()`.
- **Single instance**: names `Local\ConsoleSnippingTool.Instance` (mutex) and `Local\ConsoleSnippingTool.Replace` (manual-reset event, reset by the acquirer). `TryAcquireInstance` → `Mutex.TryOpen`/`new Mutex(initiallyOwned: true, name, out createdNew)` semantics; on acquisition start a background wait on the event that marshals `core.OnReplaceRequested()` onto the message loop. `SignalPreviousInstance` → set the event. `WaitForPreviousInstanceExit(timeout)` → wait for the mutex (abandoned counts as success). `TerminatePreviousInstance` → find other processes with our exe path (`Process.GetProcessesByName` filtered by `MainModule.FileName`, excluding the current id) and `Kill()`.
- **Exit** → `Application.ExitThread()`.

## Acceptance

- `dotnet build` succeeds with no warnings from the adapter; `dotnet test` still green.
- All P/Invoke declarations in one `NativeMethods` file using `LibraryImport` or `DllImport` with `SetLastError` where relevant; no `unsafe`.

## Comments
- Resolved in 28a1d42 (07: Win32 Platform adapter; branch tip 96e8b6f merges 06 and swaps in ClickSound.Wav()), merged onto integration/console-snipping-tool; build green, 78 tests green. Public surface: Win32Platform() on the STA thread, Attach(SnipperCore). Names: Local\ConsoleSnippingTool.Instance (mutex), Local\ConsoleSnippingTool.Replace (event). The adapter defers UnhookWindowsHookEx until the pending button-up is swallowed (a stray WM_RBUTTONUP would raise WM_CONTEXTMENU). Un-ignored src/ConsoleSnippingTool/Win32/ in .gitignore (the template ignores Win32/ build dirs). Open items handed to ticket 08: the second-corner capture runs inside the low-level hook callback (hook timeout risk); CTRL_CLOSE_EVENT under Windows Terminal cannot be vetoed; injected input is currently ignored; WriteConfigText failures propagate.
