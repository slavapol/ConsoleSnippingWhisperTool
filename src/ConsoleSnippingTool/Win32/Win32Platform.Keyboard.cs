using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    // Dictation Hotkey: registered like the Capture one under its own id, so the chord's key-down is swallowed
    // system-wide and a conflict reaches the core as false.

    public bool RegisterDictationHotkey(Hotkey hotkey) =>
        NativeMethods.RegisterHotKey(
            _window.Handle,
            MessageWindow.DictationHotkeyId,
            (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT,
            (uint)hotkey.VirtualKey);

    public void UnregisterDictationHotkey() => NativeMethods.UnregisterHotKey(_window.Handle, MessageWindow.DictationHotkeyId);

    // Keyboard hook (ticket 04): WH_KEYBOARD_LL, installed only while Listening, posting WM_DICTATION_KEY_UP once
    // when any key of the chord goes up and swallowing nothing. Until then no Release is ever reported.

    public void InstallKeyboardHook(Hotkey chord)
    {
    }

    public void RemoveKeyboardHook()
    {
    }
}
