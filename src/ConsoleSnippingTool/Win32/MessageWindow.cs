using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

/// The hidden message-only window on the STA thread. Hotkeys are registered against it, so it receives WM_HOTKEY;
/// the instance watch (pool thread), the mouse hook (inside GetMessage) and the console control handler (console
/// host thread) post the WM_APP messages to it so that the core runs on the message loop. Each message maps to one
/// core event.
internal sealed class MessageWindow : NativeWindow
{
    public const int CaptureHotkeyId = 1;
    public const int CancelKeyId = 2;
    public const int WM_REPLACE_REQUESTED = NativeMethods.WM_APP + 1;
    public const int WM_LEFT_BUTTON_DOWN = NativeMethods.WM_APP + 2;  // wParam = x, lParam = y, virtual-screen pixels
    public const int WM_RIGHT_BUTTON_DOWN = NativeMethods.WM_APP + 3;
    public const int WM_EXIT_REQUESTED = NativeMethods.WM_APP + 4;

    // Resolved per message: the core is attached to the platform after this window exists.
    private readonly Func<SnipperCore> _core;

    public MessageWindow(Func<SnipperCore> core)
    {
        _core = core;
        CreateHandle(new CreateParams { Parent = NativeMethods.HWND_MESSAGE });
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_HOTKEY when (int)m.WParam == CaptureHotkeyId:
                _core().OnHotkey();
                break;
            case NativeMethods.WM_HOTKEY when (int)m.WParam == CancelKeyId:
                _core().OnCancelKey();
                break;
            case WM_REPLACE_REQUESTED:
                _core().OnReplaceRequested();
                break;
            case WM_LEFT_BUTTON_DOWN:
                _core().OnLeftButtonDown((int)m.WParam, (int)m.LParam);
                break;
            case WM_RIGHT_BUTTON_DOWN:
                _core().OnRightButtonDown();
                break;
            case WM_EXIT_REQUESTED:
                _core().OnExit();
                break;
        }

        base.WndProc(ref m);
    }
}
