using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

public class ConsoleTests
{
    /// The header Show prints before the ring, for the default Hotkeys.
    private static readonly string[] Header =
    [
        "WriteConsoleLine(ConsoleSnippingTool)",
        "WriteConsoleLine(Press Ctrl+Win+Z to capture)",
        "WriteConsoleLine(Hold Ctrl+Win+X to dictate)",
        "WriteConsoleLine(Esc or right-click cancels a capture; Esc discards a dictation)",
        "WriteConsoleLine()",
    ];

    /// A pinned clock, so every logged line carries the same known timestamp.
    private static DateTime At_09_05_07() => new(2026, 10, 28, 9, 5, 7);

    /// What Start logs when another app owns the Hotkey: the loggable event the existing Startup offers.
    private const string HotkeyTaken = "Hotkey Ctrl+Win+Z is already taken by another app; edit config.json and relaunch";

    private const string HotkeyTakenLine = $"09:05:07 {HotkeyTaken}";

    private static bool IsConsoleLine(string call) => call.StartsWith("WriteConsoleLine(", StringComparison.Ordinal);

    [Fact]
    public void Show_opens_the_Console_prints_the_header_and_enables_Hide()
    {
        var fake = new FakePlatform { Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);
        core.Start();
        fake.Calls.Clear();

        core.OnShow();

        string[] expected =
        [
            "ShowConsole()",
            .. Header,
            "SetTrayMenuState(showEnabled:false,hideEnabled:true)",
        ];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void The_header_names_the_configured_Hotkeys()
    {
        var fake = new FakePlatform
        {
            ConfigText = ConfigJson.With(hotkey: "Ctrl+Shift+F5", dictationHotkey: "Ctrl+Alt+D"),
            Environment = ApiKey.InEnvironment,
        };
        var core = new SnipperCore(fake);
        core.Start();
        fake.Calls.Clear();

        core.OnShow();

        string[] expected =
        [
            "WriteConsoleLine(ConsoleSnippingTool)",
            "WriteConsoleLine(Press Ctrl+Shift+F5 to capture)",
            "WriteConsoleLine(Hold Ctrl+Alt+D to dictate)",
            "WriteConsoleLine(Esc or right-click cancels a capture; Esc discards a dictation)",
            "WriteConsoleLine()",
        ];
        Assert.Equal(expected, fake.Calls.Where(IsConsoleLine));
    }

    [Fact]
    public void A_line_logged_while_shown_is_written_at_once_with_its_timestamp_and_only_once()
    {
        var fake = new FakePlatform { RegisterHotkeyResult = false, Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake, At_09_05_07);
        core.OnShow();
        fake.Calls.Clear();

        core.Start();

        Assert.Equal([$"WriteConsoleLine({HotkeyTakenLine})"], fake.Calls.Where(IsConsoleLine));
    }

    [Fact]
    public void Lines_logged_while_hidden_follow_the_header_on_the_next_Show_oldest_first()
    {
        var fake = new FakePlatform { RegisterHotkeyResult = false, Environment = ApiKey.InEnvironment };
        fake.AcquireResults.Enqueue(false);
        var core = new SnipperCore(fake, At_09_05_07);
        core.Start();
        fake.Calls.Clear();

        core.OnShow();

        string[] expected =
        [
            "ShowConsole()",
            .. Header,
            "WriteConsoleLine(09:05:07 Replaced previous instance)",
            $"WriteConsoleLine({HotkeyTakenLine})",
            "SetTrayMenuState(showEnabled:false,hideEnabled:true)",
        ];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void Hide_closes_the_Console_and_enables_Show_and_the_next_Show_brings_the_log_back()
    {
        var fake = new FakePlatform { RegisterHotkeyResult = false, Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake, At_09_05_07);
        core.Start();
        core.OnShow();
        fake.Calls.Clear();

        core.OnHide();
        core.OnShow();

        string[] expected =
        [
            "HideConsole()",
            "SetTrayMenuState(showEnabled:true,hideEnabled:false)",
            "ShowConsole()",
            .. Header,
            $"WriteConsoleLine({HotkeyTakenLine})",
            "SetTrayMenuState(showEnabled:false,hideEnabled:true)",
        ];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void Show_while_already_shown_does_nothing()
    {
        var fake = new FakePlatform { Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);
        core.Start();
        core.OnShow();
        fake.Calls.Clear();

        core.OnShow();

        Assert.Empty(fake.Calls);
    }

    [Fact]
    public void Hide_while_already_hidden_does_nothing()
    {
        var fake = new FakePlatform { Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);
        core.Start();
        fake.Calls.Clear();

        core.OnHide();

        Assert.Empty(fake.Calls);
    }

    [Fact]
    public void The_ring_keeps_the_last_200_lines_so_Show_after_250_replays_lines_51_to_250()
    {
        var second = 0;
        DateTime OneSecondPerLine() => new DateTime(2026, 10, 28, 0, 0, 0).AddSeconds(second++);
        var fake = new FakePlatform { Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake, OneSecondPerLine);
        core.Start();
        for (var i = 0; i < 125; i++)
        {
            core.OnHotkey();    // logs "Armed"
            core.OnCancelKey(); // logs "Cancelled (Esc)"
        }
        fake.Calls.Clear();

        core.OnShow();

        var replayed = fake.Calls.Where(IsConsoleLine).Skip(Header.Length).ToList();
        Assert.Equal(200, replayed.Count);
        Assert.Equal("WriteConsoleLine(00:00:50 Armed)", replayed[0]);
        Assert.Equal("WriteConsoleLine(00:04:09 Cancelled (Esc))", replayed[^1]);
    }

    [Fact]
    public void Exit_while_shown_hides_the_Console_before_releasing_the_Hotkeys_and_the_tray_icon()
    {
        var fake = new FakePlatform { Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);
        core.Start();
        core.OnShow();
        fake.Calls.Clear();

        core.OnExit();

        string[] expected =
        [
            "HideConsole()",
            "SetTrayMenuState(showEnabled:true,hideEnabled:false)",
            "UnregisterHotkey()",
            "UnregisterDictationHotkey()",
            "RemoveTrayIcon()",
            "Exit()",
        ];
        Assert.Equal(expected, fake.Calls);
    }
}
