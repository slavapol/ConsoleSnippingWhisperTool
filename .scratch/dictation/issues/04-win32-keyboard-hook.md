# 04 Win32: Dictation Hotkey release through a low-level keyboard hook

Status: ready-for-agent
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
