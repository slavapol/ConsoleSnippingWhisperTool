using System.Collections.Concurrent;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

/// The hidden message-only window on the STA thread. Hotkeys are registered against it, so it receives WM_HOTKEY;
/// the instance watch (pool thread), the mouse and keyboard hooks (inside GetMessage) and the console control handler
/// (console host thread) post the WM_APP messages to it so that the core runs on the message loop. Each message maps to one
/// core event, except WM_RUN_POSTED, which runs whatever actions other threads handed to <see cref="Post"/>.
internal sealed class MessageWindow : NativeWindow
{
    public const int CaptureHotkeyId = 1;
    public const int CancelKeyId = 2;
    public const int MicrophoneHotkeyId = 3;
    public const int PlaybackHotkeyId = 4;
    public const int WM_REPLACE_REQUESTED = NativeMethods.WM_APP + 1;
    public const int WM_LEFT_BUTTON_DOWN = NativeMethods.WM_APP + 2;  // wParam = x, lParam = y, virtual-screen pixels
    public const int WM_RIGHT_BUTTON_DOWN = NativeMethods.WM_APP + 3;
    public const int WM_EXIT_REQUESTED = NativeMethods.WM_APP + 4;
    public const int WM_MICROPHONE_KEY_UP = NativeMethods.WM_APP + 5;
    public const int WM_RUN_POSTED = NativeMethods.WM_APP + 6;
    public const int WM_MICROPHONE_CANCEL = NativeMethods.WM_APP + 7;  // Esc swallowed by the keyboard hook while Listening

    // Resolved per message: the core is attached to the platform after this window exists.
    private readonly Func<SnipperCore> _core;

    // Actions handed to Post, run in order by the WM_RUN_POSTED handler.
    private readonly ConcurrentQueue<Action> _posted = new();

    public MessageWindow(Func<SnipperCore> core)
    {
        _core = core;
        CreateHandle(new CreateParams { Parent = NativeMethods.HWND_MESSAGE });
    }

    /// Runs the action on the message loop, after the current message if called from it. Safe from any thread: the
    /// transport (ticket 06) and the keyboard hook (ticket 04) hand results to the core this way.
    public void Post(Action action)
    {
        _posted.Enqueue(action);
        NativeMethods.PostMessageW(Handle, WM_RUN_POSTED, 0, 0);
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
            case NativeMethods.WM_HOTKEY when (int)m.WParam == MicrophoneHotkeyId:
                _core().OnMicrophoneHotkey();
                break;
            case NativeMethods.WM_HOTKEY when (int)m.WParam == PlaybackHotkeyId:
                _core().OnPlaybackHotkey();
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
            case WM_MICROPHONE_KEY_UP:
                _core().OnMicrophoneKeyUp();
                break;
            case WM_MICROPHONE_CANCEL:
                _core().OnCancelKey();
                break;
            case WM_RUN_POSTED:
                RunPosted();
                break;
        }

        base.WndProc(ref m);
    }

    /// Drains the queue: one WM_RUN_POSTED per Post, but a handler that runs several is harmless.
    private void RunPosted()
    {
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
    }
}
