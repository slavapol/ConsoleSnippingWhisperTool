using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// A Dictation on Playback, driven through the core's public events and observed only through the fake Platform's
// recorded calls and the log ring that Show replays (Playback Dictation spec: Testing Decisions). The clock is a field
// the test advances, so the Tap threshold and the wait from Release to Copy are measured, not guessed.
public class PlaybackTests
{
    /// The injected clock; starts at a pinned time and moves only when a test says so.
    private DateTime _now = new(2026, 10, 30, 14, 2, 7);

    private void Advance(TimeSpan span) => _now += span;

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    // Listening

    [Fact]
    public void The_Playback_Hotkey_while_Idle_opens_Playback_starts_the_Playback_cap_warms_up_the_Service_and_plays_Ready_with_no_keyboard_hook()
    {
        var (fake, core) = Started();

        core.OnPlaybackHotkey();

        string[] expected =
        [
            "TryOpenSource(Playback)",
            "StartTimer(600s)",
            "WarmUpService()",
            "PlaySound(Ready)",
        ];
        Assert.Equal(expected, fake.Calls);
        Assert.Equal(["14:02:07 Listening (playback: Test Device)"], Logged(fake, core));
    }

    [Fact]
    public void Playback_that_does_not_open_is_reported_with_a_balloon_and_nothing_else_starts()
    {
        var (fake, core) = Started(new FakePlatform { Environment = ApiKey.InEnvironment, SourceOpens = false });

        core.OnPlaybackHotkey();

        string[] expected = ["TryOpenSource(Playback)", "ShowBalloon(ConsoleSnippingTool: Playback could not be opened)"];
        Assert.Equal(expected, fake.Calls);
        Assert.Equal(["14:02:07 Playback could not be opened"], Logged(fake, core));
    }

    [Fact]
    public void The_Playback_Hotkey_without_an_API_key_repeats_the_startup_balloon_and_never_opens_Playback()
    {
        var (fake, core) = Started(new FakePlatform()); // no key in config.json or the environment

        core.OnPlaybackHotkey();

        Assert.Equal(["ShowBalloon(ConsoleSnippingTool: Dictation needs an API key: set openAi.apiKey in config.json or the OPENAI_API_KEY environment variable)"], fake.Calls);
    }

    [Fact]
    public void The_Listening_line_names_the_playback_device_the_adapter_opened()
    {
        var (fake, core) = Started(new FakePlatform
        {
            Environment = ApiKey.InEnvironment,
            DeviceName = "Headset Earphone (Jabra Evolve2 65)",
        });

        core.OnPlaybackHotkey();

        Assert.Equal(["14:02:07 Listening (playback: Headset Earphone (Jabra Evolve2 65))"], Logged(fake, core));
    }

    [Fact]
    public void The_Listening_line_leaves_the_name_out_when_the_adapter_reports_none()
    {
        var (fake, core) = Started(new FakePlatform { Environment = ApiKey.InEnvironment, DeviceName = "" });

        core.OnPlaybackHotkey();

        Assert.Equal(["14:02:07 Listening (playback)"], Logged(fake, core));
    }

    // Helpers

    /// A started Instance with an API key, Idle, with the startup calls cleared away.
    private (FakePlatform Fake, SnipperCore Core) Started(FakePlatform? fake = null)
    {
        fake ??= new FakePlatform { Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake, () => _now);
        core.Start();
        fake.Calls.Clear();
        return (fake, core);
    }

    /// The log ring as Show replays it, timestamps included. Call it last, because Show itself adds calls.
    private static List<string> Logged(FakePlatform fake, SnipperCore core) => ConsoleReplay.Logged(fake, core);
}
