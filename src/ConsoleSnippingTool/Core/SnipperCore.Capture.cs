namespace ConsoleSnippingTool.Core;

public sealed partial class SnipperCore
{
    // Stubs; ticket 03 implements the Capture flow in this file.

    public void OnHotkey()
    {
    }

    public void OnCancelKey()
    {
    }

    /// Returns true when the click was consumed (the adapter then swallows it and the matching up).
    public bool OnLeftButtonDown(int x, int y) => false;

    /// Returns true when the click cancelled an Armed Capture (the adapter then swallows it and the matching up).
    public bool OnRightButtonDown() => false;

    public void OnTimerElapsed()
    {
    }

    /// Leaves Armed or FirstCorner without a Capture. Ticket 03 completes it (hook, Esc, timer, log line).
    private void Cancel(string reason)
    {
        _state = State.Idle;
    }
}
