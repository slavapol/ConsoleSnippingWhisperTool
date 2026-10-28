using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ConsoleSnippingTool.Core;
using Region = ConsoleSnippingTool.Core.Region;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    // Kept in fields so the delegate handed to SetWindowsHookEx is never collected.
    private readonly NativeMethods.LowLevelMouseProc _mouseProc;
    private readonly System.Windows.Forms.Timer _armedTimer;
    private nint _mouseHook;

    // A button-down and its matching button-up are swallowed as a pair. The core removes the hook while handling the
    // posted down, which may be before the up has arrived, so that removal waits until the pair is complete.
    private bool _swallowNextLeftUp;
    private bool _swallowNextRightUp;
    private bool _removeHookAfterPair;

    // Hotkeys

    public bool RegisterHotkey(Hotkey hotkey) =>
        NativeMethods.RegisterHotKey(
            _window.Handle,
            MessageWindow.CaptureHotkeyId,
            (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT,
            (uint)hotkey.VirtualKey);

    public void UnregisterHotkey() => NativeMethods.UnregisterHotKey(_window.Handle, MessageWindow.CaptureHotkeyId);

    public void RegisterCancelKey() =>
        NativeMethods.RegisterHotKey(_window.Handle, MessageWindow.CancelKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_ESCAPE);

    public void UnregisterCancelKey() => NativeMethods.UnregisterHotKey(_window.Handle, MessageWindow.CancelKeyId);

    // Mouse hook

    public void InstallMouseHook()
    {
        // Always a fresh hook. One left installed to finish a pair may be waiting for an up that will never come:
        // Windows removes a low-level hook silently when it times out, leaving a stale handle and stuck flags.
        Unhook();
        _swallowNextLeftUp = false;
        _swallowNextRightUp = false;
        _mouseHook = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_MOUSE_LL, _mouseProc, NativeMethods.GetModuleHandleW(null), 0);
    }

    public void RemoveMouseHook()
    {
        if (_swallowNextLeftUp || _swallowNextRightUp)
        {
            _removeHookAfterPair = true;
            return;
        }

        Unhook();
    }

    private void Unhook()
    {
        if (_mouseHook == 0)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_mouseHook);
        _mouseHook = 0;
        _removeHookAfterPair = false;
    }

    private void CompletePendingRemoval()
    {
        if (_removeHookAfterPair && !_swallowNextLeftUp && !_swallowNextRightUp)
        {
            Unhook();
        }
    }

    /// Runs on the STA thread for every mouse event while installed, from inside the message loop's GetMessage, and
    /// must return within the system's low-level hook timeout: past it the input is delivered to the window under
    /// the cursor whatever is returned, and the hook may be dropped. So it decides nothing and calls nothing that
    /// could take time: it swallows every left and right button down with its matching up, and posts the down to the
    /// message window, where the core handles it after this has returned. That equals the core's own answer: it
    /// installs the hook only in Armed and FirstCorner, exactly the states in which OnLeftButtonDown and
    /// OnRightButtonDown return true (swallow), so while the hook is installed every such click is one the core
    /// would have consumed. A posted call that arrives after the core has gone Idle (a cancel raced with the click)
    /// is harmless: the core returns false and does nothing. Injected input is not filtered out: key and mouse
    /// remapping tools send it, and their users must still be able to capture. Nothing here may log or allocate.
    private nint MouseHookCallback(int nCode, nint wParam, ref NativeMethods.MSLLHOOKSTRUCT lParam)
    {
        if (nCode == NativeMethods.HC_ACTION)
        {
            switch ((int)wParam)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    _swallowNextLeftUp = true;
                    NativeMethods.PostMessageW(_window.Handle, MessageWindow.WM_LEFT_BUTTON_DOWN, lParam.pt.x, lParam.pt.y);
                    return 1;

                case NativeMethods.WM_LBUTTONUP when _swallowNextLeftUp:
                    _swallowNextLeftUp = false;
                    CompletePendingRemoval();
                    return 1;

                case NativeMethods.WM_RBUTTONDOWN:
                    _swallowNextRightUp = true;
                    NativeMethods.PostMessageW(_window.Handle, MessageWindow.WM_RIGHT_BUTTON_DOWN, 0, 0);
                    return 1;

                case NativeMethods.WM_RBUTTONUP when _swallowNextRightUp:
                    _swallowNextRightUp = false;
                    CompletePendingRemoval();
                    return 1;
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, ref lParam);
    }

    // Armed timer (one shot, on the message loop)

    public void StartArmedTimer(TimeSpan duration)
    {
        _armedTimer.Stop();
        _armedTimer.Interval = (int)duration.TotalMilliseconds;
        _armedTimer.Start();
    }

    public void StopArmedTimer() => _armedTimer.Stop();

    private void OnArmedTimerTick(object? sender, EventArgs e)
    {
        _armedTimer.Stop();
        AttachedCore.OnTimerElapsed();
    }

    // Screen to clipboard

    /// Only the clipboard write maps to false (the core reports it as a refused clipboard). A failure to read the
    /// screen propagates instead: the core is already Idle by this call, and the message loop's handler reports it
    /// as an unhandled error. The bitmap is disposed either way.
    public bool TryCaptureToClipboard(Region region)
    {
        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(region.Left, region.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        png.Position = 0;

        var data = new DataObject();
        data.SetData(DataFormats.Bitmap, true, bitmap);
        data.SetData("PNG", false, png);
        try
        {
            Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 50);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }
}
