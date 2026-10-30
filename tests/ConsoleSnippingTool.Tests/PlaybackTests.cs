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

    // The whole story

    [Fact]
    public void A_Playback_Dictation_is_Ready_on_the_first_press_Sent_on_the_second_and_Done_once_the_Transcript_is_on_the_clipboard_with_one_log_line_per_event()
    {
        var (fake, core) = Started();

        core.OnPlaybackHotkey();
        Advance(TimeSpan.FromSeconds(3));
        core.OnPlaybackHotkey();
        Advance(Ms(900));
        core.OnTranscript(1, "we ship on Friday", ["en"]);

        string[] expected =
        [
            "TryOpenSource(Playback)",
            "StartTimer(600s)",
            "WarmUpService()",
            "PlaySound(Ready)",
            "StopTimer()",
            "CloseSource()",
            "PlaySound(Sent)",
            "BeginTranscription(seq:1,clip:3s,model:gpt-transcribe,languages:en|ru,prompt:null,timeout:21s)",
            "TryCopyText(we ship on Friday)",
            "PlaySound(Done)",
        ];
        Assert.Equal(expected, fake.Calls);
        string[] expectedLog =
        [
            "14:02:07 Listening (playback: Test Device)",
            "14:02:10 Sent 3.0 s of audio",
            "14:02:10 Copied 17 chars in 0.9 s [en]: we ship on Friday",
        ];
        Assert.Equal(expectedLog, Logged(fake, core));
    }

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

    // Release

    [Fact]
    public void A_second_press_at_499_ms_is_a_Tap_that_closes_Playback_and_sends_nothing()
    {
        var (fake, core) = Listening();
        Advance(Ms(499));

        core.OnPlaybackHotkey();

        Assert.Equal(["StopTimer()", "CloseSource()"], fake.Calls);
        Assert.Equal(["14:02:07 Listening (playback: Test Device)", "14:02:07 Tap ignored"], Logged(fake, core));
    }

    [Fact]
    public void A_second_press_at_500_ms_is_a_Release_that_sends_the_clip()
    {
        var (fake, core) = Listening();
        Advance(Ms(500));

        core.OnPlaybackHotkey();

        string[] expected =
        [
            "StopTimer()",
            "CloseSource()",
            "PlaySound(Sent)",
            "BeginTranscription(seq:1,clip:3s,model:gpt-transcribe,languages:en|ru,prompt:null,timeout:21s)",
        ];
        Assert.Equal(expected, fake.Calls);
    }

    [Fact]
    public void The_Playback_cap_is_a_Release_whose_log_line_says_so()
    {
        var (fake, core) = Listening();
        fake.SourceClip = Audio(TimeSpan.FromSeconds(600));
        Advance(TimeSpan.FromSeconds(600));

        core.OnTimerElapsed();

        string[] expected =
        [
            "StopTimer()",
            "CloseSource()",
            "PlaySound(Sent)",
            "BeginTranscription(seq:1,clip:600s,model:gpt-transcribe,languages:en|ru,prompt:null,timeout:120s)",
        ];
        Assert.Equal(expected, fake.Calls);
        Assert.Equal(["14:02:07 Listening (playback: Test Device)", "14:12:07 Sent 600.0 s of audio (cap)"], Logged(fake, core));
    }

    [Fact]
    public void The_Playback_cap_is_never_a_Tap()
    {
        var (fake, core) = Listening();
        Advance(Ms(400));

        core.OnTimerElapsed();

        Assert.Contains("PlaySound(Sent)", fake.Calls);
        Assert.Equal("14:02:07 Sent 3.0 s of audio (cap)", Logged(fake, core)[^1]);
    }

    // Esc and key-ups belong to the Microphone

    [Fact]
    public void A_stale_key_up_or_Esc_while_Listening_on_Playback_touches_nothing_and_the_next_press_is_still_the_Release()
    {
        var (fake, core) = Listening();
        Advance(TimeSpan.FromSeconds(3));

        core.OnDictationKeyUp();
        core.OnCancelKey();

        Assert.Empty(fake.Calls);
        core.OnPlaybackHotkey();
        Assert.Contains("PlaySound(Sent)", fake.Calls);
        Assert.Equal(["14:02:07 Listening (playback: Test Device)", "14:02:10 Sent 3.0 s of audio"], Logged(fake, core));
    }

    // One job at a time

    [Theory]
    [InlineData("Armed", "Playback hotkey ignored while armed")]
    [InlineData("FirstCorner", "Playback hotkey ignored while armed")]
    [InlineData("MicrophoneListening", "Playback hotkey ignored while listening")]
    [InlineData("MicrophoneTranscribing", "Playback hotkey ignored while transcribing")]
    [InlineData("PlaybackTranscribing", "Playback hotkey ignored while transcribing")]
    public void The_Playback_Hotkey_during_a_Capture_a_Microphone_Dictation_or_a_Transcription_is_one_log_line_and_no_call(string state, string line)
    {
        var (fake, core) = In(state);

        core.OnPlaybackHotkey();

        Assert.Empty(fake.Calls);
        Assert.Equal($"14:02:10 {line}", Logged(fake, core)[^1]);
    }

    [Theory]
    [InlineData("PlaybackListening", "Microphone hotkey ignored while listening")]
    [InlineData("PlaybackTranscribing", "Microphone hotkey ignored while transcribing")]
    public void The_Microphone_Hotkey_during_a_Playback_Dictation_is_one_log_line_and_no_call(string state, string line)
    {
        var (fake, core) = In(state);

        core.OnDictationHotkey();

        Assert.Empty(fake.Calls);
        Assert.Equal($"14:02:10 {line}", Logged(fake, core)[^1]);
    }

    [Theory]
    [InlineData("PlaybackListening", "Capture hotkey ignored while listening")]
    [InlineData("PlaybackTranscribing", "Capture hotkey ignored while transcribing")]
    public void The_Capture_Hotkey_during_a_Playback_Dictation_is_one_log_line_and_no_call(string state, string line)
    {
        var (fake, core) = In(state);

        core.OnHotkey();

        Assert.Empty(fake.Calls);
        Assert.Equal($"14:02:10 {line}", Logged(fake, core)[^1]);
    }

    // Nothing played

    [Fact]
    public void A_Release_with_499_ms_of_audio_sends_nothing_plays_no_Sent_and_logs_Nothing_heard_nothing_played_and_the_next_press_listens_again()
    {
        var (fake, core) = Listening();
        fake.SourceClip = Audio(Ms(499));
        Advance(TimeSpan.FromSeconds(3));

        core.OnPlaybackHotkey();

        Assert.Equal(["StopTimer()", "CloseSource()"], fake.Calls);
        fake.Calls.Clear();
        core.OnPlaybackHotkey();
        Assert.Contains("PlaySound(Ready)", fake.Calls);
        string[] expectedLog =
        [
            "14:02:07 Listening (playback: Test Device)",
            "14:02:10 Nothing heard (nothing played)",
            "14:02:10 Listening (playback: Test Device)",
        ];
        Assert.Equal(expectedLog, Logged(fake, core));
    }

    [Fact]
    public void A_Release_with_500_ms_of_audio_is_sent()
    {
        var (fake, core) = Listening();
        fake.SourceClip = Audio(Ms(500));
        Advance(TimeSpan.FromSeconds(3));

        core.OnPlaybackHotkey();

        Assert.Contains("PlaySound(Sent)", fake.Calls);
        Assert.Contains("BeginTranscription(seq:1,clip:0.5s,model:gpt-transcribe,languages:en|ru,prompt:null,timeout:21s)", fake.Calls);
        Assert.Equal("14:02:10 Sent 0.5 s of audio", Logged(fake, core)[^1]);
    }

    [Fact]
    public void The_cap_with_under_500_ms_of_audio_is_Nothing_heard_too()
    {
        var (fake, core) = Listening();
        fake.SourceClip = Audio(Ms(30)); // the Ready click alone
        Advance(TimeSpan.FromSeconds(600));

        core.OnTimerElapsed();

        Assert.Equal(["StopTimer()", "CloseSource()"], fake.Calls);
        Assert.Equal("14:12:07 Nothing heard (nothing played)", Logged(fake, core)[^1]);
    }

    // Helpers

    /// That much 16 kHz audio, to the millisecond, as CloseSource returns it.
    private static AudioClip Audio(TimeSpan length) => new(new byte[(int)length.TotalMilliseconds * 16 * 2], 16000);

    /// A started Instance that has just entered Listening on the Playback Hotkey, with the entry calls cleared away.
    private (FakePlatform Fake, SnipperCore Core) Listening()
    {
        var (fake, core) = Started();
        core.OnPlaybackHotkey();
        fake.Calls.Clear();
        return (fake, core);
    }

    /// A started Instance with an API key, Idle, with the startup calls cleared away.
    private (FakePlatform Fake, SnipperCore Core) Started(FakePlatform? fake = null)
    {
        fake ??= new FakePlatform { Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake, () => _now);
        core.Start();
        fake.Calls.Clear();
        return (fake, core);
    }

    /// A started Instance in the named state with the clock three seconds past the start (so a Transcribing one was
    /// Listening well past a Tap), with everything up to that cleared away.
    private (FakePlatform Fake, SnipperCore Core) In(string state)
    {
        var (fake, core) = Started();
        var threeSeconds = TimeSpan.FromSeconds(3);
        switch (state)
        {
            case "Armed":
                core.OnHotkey();
                Advance(threeSeconds);
                break;
            case "FirstCorner":
                core.OnHotkey();
                core.OnLeftButtonDown(10, 10);
                Advance(threeSeconds);
                break;
            case "MicrophoneListening":
                core.OnDictationHotkey();
                Advance(threeSeconds);
                break;
            case "MicrophoneTranscribing":
                core.OnDictationHotkey();
                Advance(threeSeconds);
                core.OnDictationKeyUp();
                break;
            case "PlaybackListening":
                core.OnPlaybackHotkey();
                Advance(threeSeconds);
                break;
            case "PlaybackTranscribing":
                core.OnPlaybackHotkey();
                Advance(threeSeconds);
                core.OnPlaybackHotkey();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }

        fake.Calls.Clear();
        return (fake, core);
    }

    /// The log ring as Show replays it, timestamps included. Call it last, because Show itself adds calls.
    private static List<string> Logged(FakePlatform fake, SnipperCore core) => ConsoleReplay.Logged(fake, core);
}
