namespace ConsoleSnippingTool.Core;

public sealed partial class SnipperCore
{
    /// The Corner taken while Armed; meaningful only in FirstCorner.
    private Corner _firstCorner;

    private TimeSpan ArmedTimeout => TimeSpan.FromSeconds(_config.ArmedTimeoutSeconds);

    /// Arms from Idle; while Armed the Hotkey toggles the Capture off again instead of stacking.
    public void OnHotkey()
    {
        if (_state != State.Idle)
        {
            Cancel(CancelReason.Hotkey);
            return;
        }

        // The hook last of the three: once it is in, the user's clicks are swallowed, so nothing that could fail is
        // left between it and Armed.
        _platform.StartTimer(ArmedTimeout);
        _platform.RegisterCancelKey();
        _platform.InstallMouseHook();
        _platform.PlaySound(Signal.Ready);
        _state = State.Armed;
        Log("Armed");
    }

    /// Esc; registered only while Armed, so an Idle call is a stray one.
    public void OnCancelKey()
    {
        if (_state != State.Idle)
        {
            Cancel(CancelReason.Escape);
        }
    }

    /// Returns true when the click was consumed (the adapter then swallows it and the matching up).
    public bool OnLeftButtonDown(int x, int y)
    {
        switch (_state)
        {
            case State.Armed:
                TakeFirstCorner(x, y);
                return true;
            case State.FirstCorner:
                Capture(Region.FromCorners(_firstCorner, new Corner(x, y)));
                return true;
            default:
                return false;
        }
    }

    /// Returns true when the click cancelled an Armed Capture (the adapter then swallows it and the matching up).
    public bool OnRightButtonDown()
    {
        if (_state == State.Idle)
        {
            return false;
        }

        Cancel(CancelReason.RightClick);
        return true;
    }

    /// The Armed timer; stopped on leaving Armed, so an Idle call is a late one.
    public void OnTimerElapsed()
    {
        if (_state != State.Idle)
        {
            Cancel(CancelReason.Timeout);
        }
    }

    private void TakeFirstCorner(int x, int y)
    {
        _firstCorner = new Corner(x, y);
        _platform.StartTimer(ArmedTimeout);
        _platform.PlaySound(Signal.Corner);
        _state = State.FirstCorner;
        Log($"Corner 1 at {x},{y}");
    }

    private void Capture(Region region)
    {
        Disarm();
        if (_platform.TryCaptureToClipboard(region))
        {
            _platform.PlaySound(Signal.Done);
            Log($"Captured {region.Width}×{region.Height} → clipboard");
            return;
        }

        Report($"Clipboard refused the {region.Width}×{region.Height} capture; nothing was copied");
    }

    /// What ended an Armed Capture without an image; OnExit supplies Exit.
    private enum CancelReason { Escape, RightClick, Hotkey, Timeout, Exit }

    /// Leaves Armed or FirstCorner without a Capture: silent, nothing copied.
    private void Cancel(CancelReason reason)
    {
        Disarm();
        Log(reason switch
        {
            CancelReason.Escape => "Cancelled (Esc)",
            CancelReason.RightClick => "Cancelled (right-click)",
            CancelReason.Hotkey => "Cancelled (hotkey)",
            CancelReason.Timeout => "Timed out",
            CancelReason.Exit => "Cancelled (exit)",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        });
    }

    /// Stops reading the user's input: hook off, Esc released, timer stopped, Idle. The tail of every Capture and cancel.
    private void Disarm()
    {
        _platform.RemoveMouseHook();
        _platform.UnregisterCancelKey();
        _platform.StopTimer();
        _state = State.Idle;
    }
}
