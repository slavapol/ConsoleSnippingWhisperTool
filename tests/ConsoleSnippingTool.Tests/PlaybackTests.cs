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
