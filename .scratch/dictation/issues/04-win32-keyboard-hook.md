# 04 Win32: Dictation Hotkey release through a low-level keyboard hook

Status: resolved
Blocked by: 01

Spec: `.scratch/dictation/spec.md` → Hotkey, release and Tap. ADR-0002 for the hook discipline. Notes: `insert-and-hooks.md` in the shared notes directory (section "Low-level keyboard hook").

## Goal

`InstallKeyboardHook(chord)` and `RemoveKeyboardHook()` in `Win32Platform.Keyboard.cs` do what the seam promises: while installed, the first key-up of the chord's main key or of any modifier belonging to the chord posts `WM_DICTATION_KEY_UP` to the message window once, and nothing is swallowed. P/Invoke declarations go in a new `NativeMethods.Keyboard.cs` (partial). No other file changes.

## Behaviour

- `SetWindowsHookExW(WH_KEYBOARD_LL, proc, GetModuleHandleW(null), 0)`; the delegate lives in a field so it is not collected; install always unhooks a stale handle first, as the mouse hook does.
- The callback does nothing but inspect `KBDLLHOOKSTRUCT.vkCode` on `WM_KEYUP`/`WM_SYSKEYUP` and `PostMessageW`; it always returns `CallNextHookEx`. Keys that count: the chord's `VirtualKey`; for `Control` → VK_CONTROL, VK_LCONTROL, VK_RCONTROL; `Alt` → VK_MENU, VK_LMENU, VK_RMENU; `Shift` → VK_SHIFT, VK_LSHIFT, VK_RSHIFT; `Win` → VK_LWIN, VK_RWIN.
- Post at most once per installation (a flag reset on install) so two modifiers released together do not produce two events; the core ignores a second one anyway.
- `RemoveKeyboardHook` unhooks immediately; there is no pair to wait for.

## Tests

None at the unit level: adapter code without logic. Build must be warning-free. Record in Comments how it was exercised, if at all (a short manual check from a debug run is enough: press and hold, release Win first, release X first).

## Acceptance

`dotnet build` 0 warnings; `dotnet test` green (unchanged count); `IPlatform` unchanged.

## Comments
- Built on branch `worktree-agent-a5f4154851b8ea80c` in one `04:` commit; `dotnet build` 0 warnings, `dotnet test` 88 green, unchanged: adapter code with no unit tests, per the ticket. `InstallKeyboardHook` first removes any stale hook, resets the once-per-installation flag, stores the chord's main key as a VK code and its modifiers as flags, and installs `WH_KEYBOARD_LL` with the delegate kept in a field (created on the first install, so `Win32Platform.cs` is untouched). The callback posts `WM_DICTATION_KEY_UP` on the first `WM_KEYUP`/`WM_SYSKEYUP` whose `vkCode` is the chord's key or any generic, left or right code of a modifier the chord holds (`VK_CONTROL`/`VK_LCONTROL`/`VK_RCONTROL`, `VK_MENU`/`VK_LMENU`/`VK_RMENU`, `VK_SHIFT`/`VK_LSHIFT`/`VK_RSHIFT`, `VK_LWIN`/`VK_RWIN`), posts nothing for later key-ups of the same installation, and always returns `CallNextHookEx`, so nothing is swallowed. `RemoveKeyboardHook` unhooks immediately. The P/Invoke sits in the new partial `NativeMethods.Keyboard.cs`: `WM_KEYUP`, `WM_SYSKEYUP`, `WH_KEYBOARD_LL`, the eleven VK constants, `KBDLLHOOKSTRUCT`, `LowLevelKeyboardProc`, and `SetWindowsHookExW`/`CallNextHookEx` overloads typed for them; `UnhookWindowsHookEx` and `GetModuleHandleW` are shared with the mouse hook. `MOD_NOREPEAT` on the Dictation Hotkey is kept: the Release comes from the hook, not from autorepeat. `IPlatform` unchanged. Not exercised: the hook was never installed on this machine (live desktop; no debug run, no injected input), so this is verified by build only. The manual check from the ticket (hold, release Win first; hold, release X first; an Alt chord for the `WM_SYSKEYUP` path) is open for whoever next runs the exe.
- Resolved: branch tip d110688 fast-forwarded onto integration/dictation; build 0 warnings, 88 tests green on integration.
- Code review fix (2026-10-29): Esc while Listening never fired. The core registered Esc as a plain hotkey (`RegisterCancelKey`, the Armed mechanism), but `RegisterHotKey` fires only on an exact modifier match, and while Listening the chord's modifiers are physically held: the Esc the user presses reaches Windows as Ctrl+Win+Esc, which matches no registration of ours, so Windows read it as Ctrl+Esc (the Start menu) and the Dictation ran on. Now the hook, installed only while Listening, swallows the Esc key-down (and the autorepeats of a held Esc) together with its matching key-up and posts the down as the new `WM_DICTATION_CANCEL`, which `MessageWindow` routes to `core.OnCancelKey()`; `RemoveKeyboardHook` is deferred while that key-up is still owed, as the mouse hook's removal is for a click, so the pair never splits. The core's Listening transitions no longer call `RegisterCancelKey()`/`UnregisterCancelKey()` (Armed keeps them); `DictationTests` lost those two calls from every Listening call list first (8 red), then the core change made them green. The hook is still verified by build only: "hold the chord, press Esc: `Discarded (Esc)` and no Start menu" is the manual check for this fix (ticket 08, checklist item 5).
