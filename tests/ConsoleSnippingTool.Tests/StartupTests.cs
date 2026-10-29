using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

public class StartupTests
{
    [Fact]
    public void Fresh_start_writes_the_default_config_when_there_is_none()
    {
        var fake = new FakePlatform { ConfigText = null };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Equal(Normalise(ConfigJson.With()), Normalise(fake.WrittenConfigText));
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
    public void Fresh_start_acquires_the_instance_writes_config_resolves_the_key_sets_sounds_registers_the_Hotkey_and_sets_up_the_tray_in_order()
    {
        var fake = new FakePlatform { ConfigText = null, Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        string[] expected =
        [
            "TryAcquireInstance()",
            "ReadConfigText()",
            $"WriteConfigText({fake.WrittenConfigText})",
            "ReadEnvironmentVariable(OPENAI_API_KEY)",
            "TrySetSound(Ready,BuiltIn)",
            "TrySetSound(Corner,BuiltIn)",
            "TrySetSound(Done,BuiltIn)",
            "TrySetSound(Sent,BuiltIn)",
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
        var fake = new FakePlatform { RegisterHotkeyResult = false, Environment = ApiKey.InEnvironment };
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
            "TrySetSound(Sent,BuiltIn)",
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
        var fake = new FakePlatform { ConfigText = null, WriteConfigThrows = true, Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        var balloon = Assert.Single(fake.Calls, call => call.StartsWith("ShowBalloon(", StringComparison.Ordinal));
        Assert.Equal("ShowBalloon(ConsoleSnippingTool: config.json could not be written: Access to the path 'config.json' is denied; continuing with defaults)", balloon);
        Assert.Contains("RegisterHotkey(Ctrl+Win+Z)", fake.Calls);
        Assert.Contains("SetTrayMenuState(showEnabled:true,hideEnabled:false)", fake.Calls);
    }

    // The API key: resolved once at startup, from openAi.apiKey or else OPENAI_API_KEY, and never recorded anywhere.

    [Fact]
    public void A_key_in_config_is_used_without_reading_the_environment_and_without_a_report()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(apiKey: "\"sk-from-config\"") };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.DoesNotContain(fake.Calls, call => call.StartsWith("ReadEnvironmentVariable(", StringComparison.Ordinal));
        Assert.DoesNotContain(fake.Calls, call => call.StartsWith("ShowBalloon(", StringComparison.Ordinal));
        Assert.DoesNotContain("WarmUpService()", fake.Calls); // warming up the Service belongs to Listening (ticket 03), not to startup
        Assert.DoesNotContain(fake.Calls, call => call.Contains("sk-from-config", StringComparison.Ordinal));
    }

    [Fact]
    public void A_key_in_the_environment_is_read_when_config_has_none_and_nothing_is_reported()
    {
        var fake = new FakePlatform { Environment = { ["OPENAI_API_KEY"] = "sk-from-environment" } };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("ReadEnvironmentVariable(OPENAI_API_KEY)", fake.Calls);
        Assert.DoesNotContain(fake.Calls, call => call.StartsWith("ShowBalloon(", StringComparison.Ordinal));
        Assert.DoesNotContain(fake.Calls, call => call.Contains("sk-from-environment", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void No_key_in_config_or_the_environment_is_reported_right_after_the_environment_is_read_and_startup_continues(string? environmentValue)
    {
        var fake = new FakePlatform { Environment = { ["OPENAI_API_KEY"] = environmentValue } };
        var core = new SnipperCore(fake);

        core.Start();

        var balloon = Assert.Single(fake.Calls, call => call.StartsWith("ShowBalloon(", StringComparison.Ordinal));
        Assert.Equal("ShowBalloon(ConsoleSnippingTool: Dictation needs an API key: set openAi.apiKey in config.json or the OPENAI_API_KEY environment variable)", balloon);
        var read = fake.Calls.IndexOf("ReadEnvironmentVariable(OPENAI_API_KEY)");
        Assert.True(read >= 0, "the environment was consulted");
        Assert.Equal(read + 1, fake.Calls.IndexOf(balloon));
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
