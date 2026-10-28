using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

public class StartupTests
{
    /// The default config.json as the spec's Configuration section shows it.
    private const string DefaultConfigJson = """
        {
          "hotkey": "Ctrl+Win+Z",
          "armedTimeoutSeconds": 30,
          "sounds": {
            "ready": null,
            "corner": null,
            "done": null
          }
        }
        """;

    [Fact]
    public void Fresh_start_writes_the_default_config_when_there_is_none()
    {
        var fake = new FakePlatform { ConfigText = null };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Equal(Normalise(DefaultConfigJson), Normalise(fake.WrittenConfigText));
    }

    [Fact]
    public void Start_leaves_an_existing_config_alone()
    {
        var fake = new FakePlatform { ConfigText = "{}" };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.DoesNotContain(fake.Calls, call => call.StartsWith("WriteConfigText(", StringComparison.Ordinal));
    }

    [Fact]
    public void Fresh_start_acquires_the_instance_writes_config_sets_sounds_registers_the_Hotkey_and_sets_up_the_tray_in_order()
    {
        var fake = new FakePlatform { ConfigText = null };
        var core = new SnipperCore(fake);

        core.Start();

        string[] expected =
        [
            "TryAcquireInstance()",
            "ReadConfigText()",
            $"WriteConfigText({fake.WrittenConfigText})",
            "TrySetSound(Ready,BuiltIn)",
            "TrySetSound(Corner,BuiltIn)",
            "TrySetSound(Done,BuiltIn)",
            "RegisterHotkey(Ctrl+Win+Z)",
            "SetTrayTooltip(ConsoleSnippingTool (Ctrl+Win+Z))",
            "SetTrayMenuState(showEnabled:true,hideEnabled:false)",
        ];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void A_previous_Instance_that_exits_in_time_is_signalled_waited_for_and_replaced_without_termination()
    {
        var fake = new FakePlatform { WaitForPreviousResult = true };
        fake.AcquireResults.Enqueue(false);
        var core = new SnipperCore(fake);

        core.Start();

        string[] expectedHandshake =
        [
            "TryAcquireInstance()",
            "SignalPreviousInstance()",
            "WaitForPreviousInstanceExit(5s)",
            "TryAcquireInstance()",
        ];
        Assert.Equal(expectedHandshake, fake.Calls.Take(expectedHandshake.Length));
        Assert.DoesNotContain("TerminatePreviousInstance()", fake.Calls);
    }

    [Fact]
    public void A_previous_Instance_that_hangs_is_terminated_after_the_wait_expires()
    {
        var fake = new FakePlatform { WaitForPreviousResult = false };
        fake.AcquireResults.Enqueue(false);
        var core = new SnipperCore(fake);

        core.Start();

        string[] expectedHandshake =
        [
            "TryAcquireInstance()",
            "SignalPreviousInstance()",
            "WaitForPreviousInstanceExit(5s)",
            "TerminatePreviousInstance()",
            "TryAcquireInstance()",
        ];
        Assert.Equal(expectedHandshake, fake.Calls.Take(expectedHandshake.Length));
    }

    [Fact]
    public void A_Hotkey_another_app_owns_is_reported_with_a_balloon_and_the_tray_is_still_set_up()
    {
        var fake = new FakePlatform { RegisterHotkeyResult = false };
        var core = new SnipperCore(fake);

        core.Start();

        var register = fake.Calls.IndexOf("RegisterHotkey(Ctrl+Win+Z)");
        var balloon = fake.Calls.FindIndex(call => call.StartsWith("ShowBalloon(", StringComparison.Ordinal));
        var tooltip = fake.Calls.IndexOf("SetTrayTooltip(ConsoleSnippingTool (Ctrl+Win+Z))");
        Assert.True(register >= 0, "Hotkey registration was attempted");
        Assert.True(balloon > register, "a balloon follows the failed registration");
        Assert.Contains("Ctrl+Win+Z", fake.Calls[balloon]);
        Assert.True(tooltip > balloon, "the tray is still set up afterwards");
        Assert.Contains("SetTrayMenuState(showEnabled:true,hideEnabled:false)", fake.Calls);
    }

    [Fact]
    public void A_signal_whose_sound_fails_to_load_falls_back_to_the_built_in_click()
    {
        var fake = new FakePlatform { SoundLoadResults = { [Signal.Done] = false } };
        var core = new SnipperCore(fake);

        core.Start();

        string[] expectedSounds =
        [
            "TrySetSound(Ready,BuiltIn)",
            "TrySetSound(Corner,BuiltIn)",
            "TrySetSound(Done,BuiltIn)",
            "TrySetSound(Done,BuiltIn)",
        ];
        Assert.Equal(expectedSounds, fake.Calls.Where(call => call.StartsWith("TrySetSound(", StringComparison.Ordinal)));
        Assert.Contains("RegisterHotkey(Ctrl+Win+Z)", fake.Calls);
    }

    [Fact]
    public void Exit_while_Idle_releases_the_Hotkey_removes_the_tray_icon_and_stops_in_that_order()
    {
        var fake = new FakePlatform();
        var core = new SnipperCore(fake);
        core.Start();
        fake.Calls.Clear();

        core.OnExit();

        string[] expected = ["UnregisterHotkey()", "RemoveTrayIcon()", "Exit()"];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void A_replace_request_from_a_newer_Instance_exits_like_Exit()
    {
        var fake = new FakePlatform();
        var core = new SnipperCore(fake);
        core.Start();
        fake.Calls.Clear();

        core.OnReplaceRequested();

        string[] expected = ["UnregisterHotkey()", "RemoveTrayIcon()", "Exit()"];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void A_config_file_that_cannot_be_written_is_reported_with_a_balloon_and_startup_continues_with_defaults()
    {
        var fake = new FakePlatform { ConfigText = null, WriteConfigThrows = true };
        var core = new SnipperCore(fake);

        core.Start();

        var balloon = Assert.Single(fake.Calls.Where(call => call.StartsWith("ShowBalloon(", StringComparison.Ordinal)));
        Assert.Equal("ShowBalloon(ConsoleSnippingTool: config.json could not be written: Access to the path 'config.json' is denied; continuing with defaults)", balloon);
        Assert.Contains("RegisterHotkey(Ctrl+Win+Z)", fake.Calls);
        Assert.Contains("SetTrayMenuState(showEnabled:true,hideEnabled:false)", fake.Calls);
    }

    [Fact]
    public void An_unhandled_exception_is_shown_as_a_balloon_and_logged_instead_of_crashing()
    {
        var fake = new FakePlatform();
        var core = new SnipperCore(fake, () => new DateTime(2026, 10, 28, 9, 5, 7));
        core.Start();
        fake.Calls.Clear();

        core.OnUnhandledException(new InvalidOperationException("boom"));
        core.OnShow();

        Assert.Equal(["ShowBalloon(ConsoleSnippingTool: Unhandled error: InvalidOperationException: boom)"], fake.Calls.TakeWhile(call => call != "ShowConsole()"));
        Assert.Contains("WriteConsoleLine(09:05:07 Unhandled error: InvalidOperationException: boom)", fake.Calls);
    }

    private static string? Normalise(string? text) => text?.ReplaceLineEndings("\n").TrimEnd();
}
