namespace ConsoleSnippingTool.Win32;

/// The hidden message-only window on the STA thread. Hotkeys are registered against it, so it receives WM_HOTKEY;
/// the instance watch (pool thread), the mouse hook (inside GetMessage) and the console control handler (console
/// host thread) post the WM_APP messages to it so that the core runs on the message loop. Each message maps to one
/// callback.
internal sealed class MessageWindow : NativeWindow
{
    public const int CaptureHotkeyId = 1;
    public const int CancelKeyId = 2;
    public const int WM_REPLACE_REQUESTED = NativeMethods.WM_APP + 1;
    public const int WM_LEFT_BUTTON_DOWN = NativeMethods.WM_APP + 2;  // wParam = x, lParam = y, virtual-screen pixels
    public const int WM_RIGHT_BUTTON_DOWN = NativeMethods.WM_APP + 3;
    public const int WM_EXIT_REQUESTED = NativeMethods.WM_APP + 4;

    private readonly Action _onHotkey;
    private readonly Action _onCancelKey;
    private readonly Action _onReplaceRequested;
    private readonly Action<int, int> _onLeftButtonDown;
    private readonly Action _onRightButtonDown;
    private readonly Action _onExitRequested;

    public MessageWindow(
        Action onHotkey,
        Action onCancelKey,
        Action onReplaceRequested,
        Action<int, int> onLeftButtonDown,
        Action onRightButtonDown,
        Action onExitRequested)
    {
        _onHotkey = onHotkey;
        _onCancelKey = onCancelKey;
        _onReplaceRequested = onReplaceRequested;
        _onLeftButtonDown = onLeftButtonDown;
        _onRightButtonDown = onRightButtonDown;
        _onExitRequested = onExitRequested;
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
            case WM_LEFT_BUTTON_DOWN:
                _onLeftButtonDown((int)m.WParam, (int)m.LParam);
                break;
            case WM_RIGHT_BUTTON_DOWN:
                _onRightButtonDown();
                break;
            case WM_EXIT_REQUESTED:
                _onExitRequested();
                break;
        }

        base.WndProc(ref m);
    }
}
