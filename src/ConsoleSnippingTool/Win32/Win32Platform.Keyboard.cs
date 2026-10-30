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

    // An Esc key-down and its matching key-up are swallowed as a pair. The core removes the hook while handling the
    // posted down, which is before the up has arrived, so that removal waits until the pair is complete, as the mouse
    // hook's does for a click.
    private bool _swallowNextEscapeUp;
    private bool _removeKeyboardHookAfterEscapeUp;

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

    // Playback Hotkey: registered the same way under its own id. Only the press matters, a second press being the
    // Release, so no hook is involved; MOD_NOREPEAT keeps a slowly released chord to one press (spec: user story 10).

    public bool RegisterPlaybackHotkey(Hotkey hotkey) =>
        NativeMethods.RegisterHotKey(
            _window.Handle,
            MessageWindow.PlaybackHotkeyId,
            (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT,
            (uint)hotkey.VirtualKey);

    public void UnregisterPlaybackHotkey() => NativeMethods.UnregisterHotKey(_window.Handle, MessageWindow.PlaybackHotkeyId);

    // Keyboard hook: WH_KEYBOARD_LL, installed by the core only while Listening, mirroring the mouse hook of ADR-0002.
    // It reports the chord's first key-up and swallows exactly one key: Esc.
    //
    // Esc cannot come through RegisterHotKey while Listening, which is how it comes while Armed. RegisterHotKey fires
    // only on an exact modifier match: an Esc registered with no modifiers matches a bare Esc and nothing else. While
    // Listening the user is physically holding the chord's modifiers (Ctrl and Win by default), so the Esc they press
    // reaches Windows as Ctrl+Win+Esc, which matches no registration of ours; Windows then reads it as Ctrl+Esc, opens
    // the Start menu, and the Dictation runs on. The hook sees the raw key event before any of that, so while it is
    // installed it swallows the Esc key-down (and the autorepeats of a held Esc) together with the matching key-up, and
    // posts the down to the message window as WM_DICTATION_CANCEL, where the core handles it as OnCancelKey after the
    // callback has returned. The cancel-key registration stays an Armed-only mechanism.

    public void InstallKeyboardHook(Hotkey chord)
    {
        // Always a fresh hook, as the mouse hook is: Windows removes a low-level hook silently when it times out,
        // leaving a stale handle, and one left installed to finish an Esc pair may be waiting for an up that never came.
        UnhookKeyboard();
        var proc = _keyboardProc ??= KeyboardHookCallback;
        _chordKey = (uint)chord.VirtualKey;
        _chordModifiers = chord.Modifiers;
        _releasePosted = false;
        _swallowNextEscapeUp = false;
        _keyboardHook = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_KEYBOARD_LL, proc, NativeMethods.GetModuleHandleW(null), 0);
    }

    /// Immediate, unless an Esc key-down has been swallowed and its key-up is still to come: then the hook stays until
    /// that up has been swallowed too, so the pair never splits. The core is Idle by then, and all the hook still does
    /// is swallow that one up; a chord key-up it posts meanwhile finds the core Idle and is ignored.
    public void RemoveKeyboardHook()
    {
        if (_swallowNextEscapeUp)
        {
            _removeKeyboardHookAfterEscapeUp = true;
            return;
        }

        UnhookKeyboard();
    }

    private void UnhookKeyboard()
    {
        if (_keyboardHook == 0)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = 0;
        _removeKeyboardHookAfterEscapeUp = false;
    }

    /// Runs on the STA thread for every key event while installed, from inside the message loop's GetMessage, under
    /// the same bounded-time rule as MouseHookCallback: it decides nothing and calls nothing that could take time. Two
    /// jobs. On the first key-up (WM_KEYUP, or WM_SYSKEYUP while Alt is down) of a key belonging to the chord it posts
    /// WM_DICTATION_KEY_UP, and the core handles the Release after this has returned; later key-ups of the same
    /// installation post nothing, so a chord whose keys go up together is one Release (the core would ignore a second
    /// anyway). On an Esc key-down it posts WM_DICTATION_CANCEL and swallows the event, and it swallows the matching
    /// key-up; the note above InstallKeyboardHook says why Esc must come this way. Every other event, matched or not,
    /// goes on to the next hook and to the focused app: the chord's key-down was already swallowed by RegisterHotKey,
    /// and the app must still see the modifiers go up or it would hold them down. Injected input is not filtered out,
    /// for the same reason as in the mouse hook. Nothing here may log or allocate.
    private nint KeyboardHookCallback(int nCode, nint wParam, ref NativeMethods.KBDLLHOOKSTRUCT lParam)
    {
        if (nCode == NativeMethods.HC_ACTION)
        {
            var message = (int)wParam;
            if (lParam.vkCode == NativeMethods.VK_ESCAPE)
            {
                switch (message)
                {
                    case NativeMethods.WM_KEYDOWN:
                    case NativeMethods.WM_SYSKEYDOWN:
                        if (!_swallowNextEscapeUp)
                        {
                            // The first down of this press; a held Esc repeats the down and is swallowed without a second post.
                            _swallowNextEscapeUp = true;
                            NativeMethods.PostMessageW(_window.Handle, MessageWindow.WM_DICTATION_CANCEL, 0, 0);
                        }

                        return 1;

                    case NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP when _swallowNextEscapeUp:
                        _swallowNextEscapeUp = false;
                        if (_removeKeyboardHookAfterEscapeUp)
                        {
                            UnhookKeyboard();
                        }

                        return 1;
                }
            }
            else if (!_releasePosted
                && message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP
                && IsChordKey(lParam.vkCode))
            {
                _releasePosted = true;
                NativeMethods.PostMessageW(_window.Handle, MessageWindow.WM_DICTATION_KEY_UP, 0, 0);
            }
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
