using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    // Kept in a field so the delegate handed to SetWindowsHookEx is never collected. Created on the first install
    // rather than in the constructor, which keeps everything about the keyboard hook in this file.
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private nint _keyboardHook;

    // The chord whose first key-up is the Release, in the form the callback compares against: the main key as a VK
    // code and the modifiers as flags. Written on install, read in the callback.
    private uint _chordKey;
    private HotkeyModifiers _chordModifiers;

    // Set by the one post of an installation so that two keys of the chord going up together post once; reset on
    // install.
    private bool _releasePosted;

    // Dictation Hotkey: registered like the Capture one under its own id, so the chord's key-down is swallowed
    // system-wide and a conflict reaches the core as false. MOD_NOREPEAT stays: the Release comes from the hook below,
    // not from autorepeat, so the repeated WM_HOTKEY messages the core would ignore anyway need not arrive.

    public bool RegisterDictationHotkey(Hotkey hotkey) =>
        NativeMethods.RegisterHotKey(
            _window.Handle,
            MessageWindow.DictationHotkeyId,
            (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT,
            (uint)hotkey.VirtualKey);

    public void UnregisterDictationHotkey() => NativeMethods.UnregisterHotKey(_window.Handle, MessageWindow.DictationHotkeyId);

    // Keyboard hook: WH_KEYBOARD_LL, installed by the core only while Listening, mirroring the mouse hook of ADR-0002.
    // It watches key-ups only and swallows nothing.

    public void InstallKeyboardHook(Hotkey chord)
    {
        // Always a fresh hook, as the mouse hook is: Windows removes a low-level hook silently when it times out,
        // leaving a stale handle.
        RemoveKeyboardHook();
        var proc = _keyboardProc ??= KeyboardHookCallback;
        _chordKey = (uint)chord.VirtualKey;
        _chordModifiers = chord.Modifiers;
        _releasePosted = false;
        _keyboardHook = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_KEYBOARD_LL, proc, NativeMethods.GetModuleHandleW(null), 0);
    }

    /// Immediate: nothing is swallowed, so unlike the mouse hook there is no down/up pair to finish first.
    public void RemoveKeyboardHook()
    {
        if (_keyboardHook == 0)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = 0;
    }

    /// Runs on the STA thread for every key event while installed, from inside the message loop's GetMessage, under
    /// the same bounded-time rule as MouseHookCallback: it decides nothing and calls nothing that could take time. On
    /// the first key-up (WM_KEYUP, or WM_SYSKEYUP while Alt is down) of a key belonging to the chord it posts
    /// WM_DICTATION_KEY_UP, and the core handles the Release after this has returned. Later key-ups of the same
    /// installation post nothing, so a chord whose keys go up together is one Release; the core would ignore a second
    /// anyway. Every event, matched or not, goes on to the next hook and to the focused app: the chord's key-down was
    /// already swallowed by RegisterHotKey, and the app must still see the modifiers go up or it would hold them down.
    /// Injected input is not filtered out, for the same reason as in the mouse hook. Nothing here may log or allocate.
    private nint KeyboardHookCallback(int nCode, nint wParam, ref NativeMethods.KBDLLHOOKSTRUCT lParam)
    {
        if (nCode == NativeMethods.HC_ACTION
            && !_releasePosted
            && (int)wParam is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP
            && IsChordKey(lParam.vkCode))
        {
            _releasePosted = true;
            NativeMethods.PostMessageW(_window.Handle, MessageWindow.WM_DICTATION_KEY_UP, 0, 0);
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, ref lParam);
    }

    /// The chord's main key, or any code of a modifier the chord holds: the generic one and the left and right ones.
    private bool IsChordKey(uint vkCode) =>
        vkCode == _chordKey
        || vkCode switch
        {
            NativeMethods.VK_CONTROL or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL
                => Holds(HotkeyModifiers.Control),
            NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU
                => Holds(HotkeyModifiers.Alt),
            NativeMethods.VK_SHIFT or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT
                => Holds(HotkeyModifiers.Shift),
            NativeMethods.VK_LWIN or NativeMethods.VK_RWIN
                => Holds(HotkeyModifiers.Win),
            _ => false,
        };

    private bool Holds(HotkeyModifiers modifier) => (_chordModifiers & modifier) != 0;
}
