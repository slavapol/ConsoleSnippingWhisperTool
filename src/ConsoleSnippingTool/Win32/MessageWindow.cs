namespace ConsoleSnippingTool.Win32;

/// The hidden message-only window on the STA thread. Hotkeys are registered against it, so it receives WM_HOTKEY;
/// the instance watch posts WM_REPLACE_REQUESTED to it from a pool thread. Each message maps to one callback.
internal sealed class MessageWindow : NativeWindow
{
    public const int CaptureHotkeyId = 1;
    public const int CancelKeyId = 2;
    public const int WM_REPLACE_REQUESTED = NativeMethods.WM_APP + 1;

    private readonly Action _onHotkey;
    private readonly Action _onCancelKey;
    private readonly Action _onReplaceRequested;

    public MessageWindow(Action onHotkey, Action onCancelKey, Action onReplaceRequested)
    {
        _onHotkey = onHotkey;
        _onCancelKey = onCancelKey;
        _onReplaceRequested = onReplaceRequested;
        CreateHandle(new CreateParams { Parent = NativeMethods.HWND_MESSAGE });
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_HOTKEY when (int)m.WParam == CaptureHotkeyId:
                _onHotkey();
                break;
            case NativeMethods.WM_HOTKEY when (int)m.WParam == CancelKeyId:
                _onCancelKey();
                break;
            case WM_REPLACE_REQUESTED:
                _onReplaceRequested();
                break;
        }

        base.WndProc(ref m);
    }
}
