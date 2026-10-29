using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// The Dictation state machine, driven through the core's public events and observed only through the fake
// Platform's recorded calls and the log ring that Show replays (spec: Testing Decisions). The clock is a field the
// test advances, so the Tap threshold and the wait from Release to Insert are measured, not guessed.
public class DictationTests
{
    /// The injected clock; starts at a pinned time and moves only when a test says so.
    private DateTime _now = new(2026, 10, 28, 9, 5, 7);

    private void Advance(TimeSpan span) => _now += span;

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    // Listening

    [Fact]
    public void Dictation_Hotkey_while_Idle_opens_the_microphone_starts_the_cap_registers_Esc_installs_the_hook_warms_up_the_Service_and_plays_Ready_in_that_order()
    {
        var (fake, core) = Started();

        core.OnDictationHotkey();

        string[] expected =
        [
            "TryOpenMicrophone()",
            "StartTimer(120s)",
            "RegisterCancelKey()",
            "InstallKeyboardHook(Ctrl+Win+X)",
            "WarmUpService()",
            "PlaySound(Ready)",
        ];
        Assert.Equal(expected, fake.Calls);
        Assert.Equal(["09:05:07 Listening"], Logged(fake, core));
    }

    [Fact]
    public void Dictation_Hotkey_without_an_API_key_repeats_the_startup_balloon_and_never_opens_the_microphone()
    {
        var (fake, core) = Started(new FakePlatform()); // no key in config.json or the environment

        core.OnDictationHotkey();

        Assert.Equal(["ShowBalloon(ConsoleSnippingTool: Dictation needs an API key: set openAi.apiKey in config.json or the OPENAI_API_KEY environment variable)"], fake.Calls);
    }

    [Fact]
    public void A_microphone_that_does_not_open_is_reported_with_a_balloon_and_nothing_else_starts()
    {
        var (fake, core) = Started(new FakePlatform { Environment = ApiKey.InEnvironment, MicrophoneOpens = false });

        core.OnDictationHotkey();

        string[] expected = ["TryOpenMicrophone()", "ShowBalloon(ConsoleSnippingTool: No microphone found)"];
        Assert.Equal(expected, fake.Calls);
        Assert.Equal(["09:05:07 No microphone found"], Logged(fake, core));
    }

    [Fact]
    public void The_cap_and_the_chord_come_from_config()
    {
        var (fake, core) = Started(new FakePlatform
        {
            ConfigText = ConfigJson.With(dictationHotkey: "Ctrl+Alt+D", listeningTimeoutSeconds: "45"),
            Environment = ApiKey.InEnvironment,
        });

        core.OnDictationHotkey();

        Assert.Contains("StartTimer(45s)", fake.Calls);
        Assert.Contains("InstallKeyboardHook(Ctrl+Alt+D)", fake.Calls);
    }

    // Release

    [Fact]
    public void Release_after_a_held_chord_removes_the_hook_releases_Esc_stops_the_cap_closes_the_microphone_plays_Sent_and_sends_the_clip_in_that_order()
    {
        var (fake, core) = Listening();
        Advance(TimeSpan.FromSeconds(3));

        core.OnDictationKeyUp();

        string[] expected =
        [
            "RemoveKeyboardHook()",
            "UnregisterCancelKey()",
            "StopTimer()",
            "CloseMicrophone()",
            "PlaySound(Sent)",
            "BeginTranscription(seq:1,clip:3s,model:gpt-transcribe,language:null,prompt:null)",
        ];
        Assert.Equal(expected, fake.Calls);
        Assert.Equal(["09:05:07 Listening", "09:05:10 Sent 3.0 s of audio"], Logged(fake, core));
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

    /// A started Instance that has just entered Listening on the Dictation Hotkey, with the entry calls cleared away.
    private (FakePlatform Fake, SnipperCore Core) Listening()
    {
        var (fake, core) = Started();
        core.OnDictationHotkey();
        fake.Calls.Clear();
        return (fake, core);
    }

    /// The header Show prints before the ring: the tool name, the two Hotkeys, the discard line and a blank one.
    private const int HeaderLines = 5;

    /// The log ring as Show replays it, timestamps included: every line after the header. Call it last, because
    /// Show itself adds calls.
    private static List<string> Logged(FakePlatform fake, SnipperCore core)
    {
        var before = fake.Calls.Count;
        core.OnShow();
        return fake.Calls
            .Skip(before + 1 + HeaderLines)
            .TakeWhile(call => call.StartsWith("WriteConsoleLine(", StringComparison.Ordinal))
            .Select(call => call["WriteConsoleLine(".Length..^1])
            .ToList();
    }
}
