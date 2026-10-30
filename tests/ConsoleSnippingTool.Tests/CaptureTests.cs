using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

public class CaptureTests
{
    [Fact]
    public void Hotkey_while_Idle_starts_the_timer_registers_Esc_installs_the_hook_and_plays_Ready_in_that_order()
    {
        // The timer first: the hook is the step that swallows the user's clicks, so nothing that could fail precedes it
        // while the hook is live.
        var (fake, core) = Started();

        core.OnHotkey();

        string[] expected = ["StartTimer(30s)", "RegisterCancelKey()", "InstallMouseHook()", "PlaySound(Ready)"];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void First_left_click_while_Armed_restarts_the_timer_plays_Corner_and_is_swallowed()
    {
        var (fake, core) = Armed();

        var swallowed = core.OnLeftButtonDown(10, 10);

        Assert.True(swallowed);
        string[] expected = ["StartTimer(30s)", "PlaySound(Corner)"];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void Second_left_click_captures_the_inclusive_Region_between_the_Corners_to_the_clipboard_and_plays_Done()
    {
        var (fake, core) = AtFirstCorner(10, 10);

        var swallowed = core.OnLeftButtonDown(5, 20);

        Assert.True(swallowed);
        string[] expected =
        [
            "RemoveMouseHook()",
            "UnregisterCancelKey()",
            "StopTimer()",
            "TryCaptureToClipboard(5,10,6x11)",
            "PlaySound(Done)",
        ];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void Corners_in_the_opposite_order_give_the_same_Region()
    {
        var (fake, core) = AtFirstCorner(5, 20);

        core.OnLeftButtonDown(10, 10);

        Assert.Contains("TryCaptureToClipboard(5,10,6x11)", fake.Calls);
    }

    [Fact]
    public void The_same_point_twice_gives_a_1x1_Region_rather_than_a_cancel()
    {
        var (fake, core) = AtFirstCorner(100, 200);

        core.OnLeftButtonDown(100, 200);

        Assert.Contains("TryCaptureToClipboard(100,200,1x1)", fake.Calls);
        Assert.Contains("PlaySound(Done)", fake.Calls);
    }

    [Fact]
    public void Corners_on_a_monitor_left_of_and_above_the_primary_give_a_Region_at_negative_coordinates()
    {
        var (fake, core) = AtFirstCorner(-1, 50);

        core.OnLeftButtonDown(-1920, -100);

        Assert.Contains("TryCaptureToClipboard(-1920,-100,1920x151)", fake.Calls);
    }

    [Theory]
    [InlineData("Esc")]
    [InlineData("right-click")]
    [InlineData("hotkey")]
    [InlineData("timeout")]
    public void Cancel_while_Armed_removes_the_hook_releases_Esc_stops_the_timer_and_plays_nothing(string trigger)
    {
        var (fake, core) = Armed();

        Cancel(core, trigger);

        string[] expected = ["RemoveMouseHook()", "UnregisterCancelKey()", "StopTimer()"];
        Assert.Equal(expected, fake.Calls);
    }

    [Theory]
    [InlineData("Esc")]
    [InlineData("right-click")]
    [InlineData("hotkey")]
    [InlineData("timeout")]
    public void Cancel_after_the_first_Corner_removes_the_hook_releases_Esc_stops_the_timer_and_copies_nothing(string trigger)
    {
        var (fake, core) = AtFirstCorner(10, 10);

        Cancel(core, trigger);

        string[] expected = ["RemoveMouseHook()", "UnregisterCancelKey()", "StopTimer()"];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void Right_click_that_cancels_is_swallowed()
    {
        var (_, armed) = Armed();
        var (_, atFirstCorner) = AtFirstCorner(10, 10);

        Assert.True(armed.OnRightButtonDown());
        Assert.True(atFirstCorner.OnRightButtonDown());
    }

    [Fact]
    public void A_clipboard_failure_plays_no_Done_and_shows_a_balloon_instead()
    {
        var (fake, core) = AtFirstCorner(10, 10);
        fake.CaptureResult = false;

        core.OnLeftButtonDown(5, 20);

        var capture = fake.Calls.IndexOf("TryCaptureToClipboard(5,10,6x11)");
        var balloon = fake.Calls.FindIndex(call => call.StartsWith("ShowBalloon(ConsoleSnippingTool: ", StringComparison.Ordinal));
        Assert.True(capture >= 0, "the capture was attempted");
        Assert.True(balloon > capture, "a balloon follows the failed capture");
        Assert.DoesNotContain("PlaySound(Done)", fake.Calls);
    }

    [Fact]
    public void Clicks_while_Idle_are_not_swallowed_and_touch_nothing()
    {
        var (fake, core) = Started();

        Assert.False(core.OnLeftButtonDown(10, 10));
        Assert.False(core.OnRightButtonDown());
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public void Esc_and_the_timer_while_Idle_do_nothing()
    {
        var (fake, core) = Started();

        core.OnCancelKey();
        core.OnTimerElapsed();

        Assert.Empty(fake.Calls);
    }

    [Fact]
    public void Exit_after_the_first_Corner_cancels_before_releasing_the_Hotkeys_and_the_tray_icon()
    {
        var (fake, core) = AtFirstCorner(10, 10);

        core.OnExit();

        string[] expected =
        [
            "RemoveMouseHook()",
            "UnregisterCancelKey()",
            "StopTimer()",
            "UnregisterHotkey()",
            "UnregisterMicrophoneHotkey()",
            "UnregisterPlaybackHotkey()",
            "RemoveTrayIcon()",
            "Exit()",
        ];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void After_a_Capture_the_Hotkey_arms_again()
    {
        var (fake, core) = AtFirstCorner(10, 10);
        core.OnLeftButtonDown(5, 20);
        fake.Calls.Clear();

        core.OnHotkey();

        string[] expected = ["StartTimer(30s)", "RegisterCancelKey()", "InstallMouseHook()", "PlaySound(Ready)"];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void After_a_cancel_the_Hotkey_arms_again()
    {
        var (fake, core) = Armed();
        core.OnCancelKey();
        fake.Calls.Clear();

        core.OnHotkey();

        string[] expected = ["StartTimer(30s)", "RegisterCancelKey()", "InstallMouseHook()", "PlaySound(Ready)"];
        Assert.Equal(expected, fake.Calls);
    }

    /// A started Instance, Idle, with the startup calls cleared away.
    private static (FakePlatform Fake, SnipperCore Core) Started()
    {
        var fake = new FakePlatform();
        var core = new SnipperCore(fake);
        core.Start();
        fake.Calls.Clear();
        return (fake, core);
    }

    /// A started Instance that has just been Armed by the Hotkey, with the arming calls cleared away.
    private static (FakePlatform Fake, SnipperCore Core) Armed()
    {
        var (fake, core) = Started();
        core.OnHotkey();
        fake.Calls.Clear();
        return (fake, core);
    }

    /// Fires one of the four cancel triggers the spec names, so a theory can cover them all.
    private static void Cancel(SnipperCore core, string trigger)
    {
        switch (trigger)
        {
            case "Esc": core.OnCancelKey(); break;
            case "right-click": core.OnRightButtonDown(); break;
            case "hotkey": core.OnHotkey(); break;
            case "timeout": core.OnTimerElapsed(); break;
            default: throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null);
        }
    }

    /// An Armed Instance that has taken its first Corner at (x, y), with everything up to that cleared away.
    private static (FakePlatform Fake, SnipperCore Core) AtFirstCorner(int x, int y)
    {
        var (fake, core) = Armed();
        core.OnLeftButtonDown(x, y);
        fake.Calls.Clear();
        return (fake, core);
    }
}
