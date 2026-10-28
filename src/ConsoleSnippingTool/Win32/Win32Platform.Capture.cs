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

    // A consumed button-down and its matching button-up are swallowed as a pair. The core removes the hook from
    // inside the down it consumes, before the up has arrived, so that removal waits until the pair is complete.
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
        _removeHookAfterPair = false;
        if (_mouseHook != 0)
        {
            return; // still installed, finishing the pair of the last consumed click
        }

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

    /// Runs on the STA thread for every mouse event while installed. Nothing here may log or allocate.
    private nint MouseHookCallback(int nCode, nint wParam, ref NativeMethods.MSLLHOOKSTRUCT lParam)
    {
        if (nCode == NativeMethods.HC_ACTION && (lParam.flags & NativeMethods.LLMHF_INJECTED) == 0)
        {
            switch ((int)wParam)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    _swallowNextLeftUp = true; // provisional, so a RemoveMouseHook from inside the call waits for the up
                    if (AttachedCore.OnLeftButtonDown(lParam.pt.x, lParam.pt.y))
                    {
                        return 1;
                    }

                    _swallowNextLeftUp = false;
                    CompletePendingRemoval();
                    break;

                case NativeMethods.WM_LBUTTONUP when _swallowNextLeftUp:
                    _swallowNextLeftUp = false;
                    CompletePendingRemoval();
                    return 1;

                case NativeMethods.WM_RBUTTONDOWN:
                    _swallowNextRightUp = true;
                    if (AttachedCore.OnRightButtonDown())
                    {
                        return 1;
                    }

                    _swallowNextRightUp = false;
                    CompletePendingRemoval();
                    break;

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

    public bool TryCaptureToClipboard(Region region)
    {
        try
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
            Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 50);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }
}
