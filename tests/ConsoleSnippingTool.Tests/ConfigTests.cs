using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

public class ConfigTests
{
    [Fact]
    public void A_configured_Hotkey_is_registered_and_shown_in_the_tray_tooltip()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(hotkey: "Ctrl+Shift+F5"), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("RegisterHotkey(Ctrl+Shift+F5)", fake.Calls);
        Assert.Contains("SetTrayTooltip(ConsoleSnippingTool (capture Ctrl+Shift+F5, dictate Ctrl+Win+X))", fake.Calls);
        Assert.Empty(Balloons(fake));
    }

    [Fact]
    public void A_configured_Dictation_Hotkey_is_registered_and_shown_in_the_tray_tooltip()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(dictationHotkey: "ctrl+alt+d"), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("RegisterDictationHotkey(Ctrl+Alt+D)", fake.Calls);
        Assert.Contains("SetTrayTooltip(ConsoleSnippingTool (capture Ctrl+Win+Z, dictate Ctrl+Alt+D))", fake.Calls);
        Assert.Empty(Balloons(fake));
    }

    [Fact]
    public void Identical_Hotkeys_register_the_Capture_one_as_written_and_the_Dictation_default_with_one_balloon()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(hotkey: "Ctrl+Alt+D", dictationHotkey: "Ctrl+Alt+D"), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("RegisterHotkey(Ctrl+Alt+D)", fake.Calls);
        Assert.Contains("RegisterDictationHotkey(Ctrl+Win+X)", fake.Calls);
        var balloon = Assert.Single(Balloons(fake));
        Assert.Equal("ShowBalloon(ConsoleSnippingTool: config.json: dictation.hotkey Ctrl+Alt+D is the same as the Capture Hotkey in effect; using default Ctrl+Win+X)", balloon);
    }

    [Fact]
    public void A_Hotkey_with_odd_case_and_spacing_is_normalised_before_registration()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(hotkey: " ctrl + win + z "), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("RegisterHotkey(Ctrl+Win+Z)", fake.Calls);
        Assert.Empty(Balloons(fake));
    }

    [Theory]
    [InlineData("Z")]           // no modifier
    [InlineData("Ctrl")]        // no key
    [InlineData("Ctrl+Z+X")]    // two keys
    [InlineData("Ctrl+Home")]   // unsupported key
    public void An_invalid_Hotkey_falls_back_to_the_default_with_one_balloon_naming_the_field(string hotkey)
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(hotkey), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("RegisterHotkey(Ctrl+Win+Z)", fake.Calls);
        Assert.Contains("SetTrayTooltip(ConsoleSnippingTool (capture Ctrl+Win+Z, dictate Ctrl+Win+X))", fake.Calls);
        var balloon = Assert.Single(Balloons(fake));
        Assert.Equal($"ShowBalloon(ConsoleSnippingTool: config.json: hotkey \"{hotkey}\" is not a valid Hotkey; using default Ctrl+Win+Z)", balloon);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("\"ten\"")]
    [InlineData("3601")]
    [InlineData("100000000")] // would overflow the adapter's millisecond timer interval
    public void A_bad_Armed_timeout_falls_back_to_30_with_a_balloon_while_a_valid_Hotkey_in_the_same_file_is_honoured(string timeout)
    {
        var json = ConfigJson.With(hotkey: "Ctrl+Shift+F5", armedTimeoutSeconds: timeout);
        var fake = new FakePlatform { ConfigText = json, Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("RegisterHotkey(Ctrl+Shift+F5)", fake.Calls);
        var balloon = Assert.Single(Balloons(fake));
        Assert.Equal($"ShowBalloon(ConsoleSnippingTool: config.json: armedTimeoutSeconds {timeout} is not an integer from 1 to 3600; using default 30)", balloon);
        Assert.Equal(30, Config.Load(json).Config.ArmedTimeoutSeconds);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(3600)]
    public void A_configured_Armed_timeout_is_what_the_Hotkey_starts_the_timer_with(int seconds)
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(armedTimeoutSeconds: seconds.ToString()), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);
        core.Start();
        fake.Calls.Clear();

        core.OnHotkey();

        Assert.Contains($"StartTimer({seconds}s)", fake.Calls);
        Assert.Empty(Balloons(fake));
    }

    [Fact]
    public void Unparseable_JSON_yields_all_defaults_and_one_balloon()
    {
        var fake = new FakePlatform { ConfigText = "{ \"hotkey\": \"Ctrl+Shift+F5\", ", Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        string[] expectedAfterConfig =
        [
            "TrySetSound(Ready,BuiltIn)",
            "TrySetSound(Corner,BuiltIn)",
            "TrySetSound(Done,BuiltIn)",
            "TrySetSound(Sent,BuiltIn)",
            "RegisterHotkey(Ctrl+Win+Z)",
            "RegisterDictationHotkey(Ctrl+Win+X)",
            "SetTrayTooltip(ConsoleSnippingTool (capture Ctrl+Win+Z, dictate Ctrl+Win+X))",
            "SetTrayMenuState(showEnabled:true,hideEnabled:false)",
        ];
        var balloon = Assert.Single(Balloons(fake));
        Assert.StartsWith("ShowBalloon(ConsoleSnippingTool: config.json could not be parsed", balloon);
        Assert.EndsWith("; using defaults)", balloon);
        Assert.Equal(expectedAfterConfig, fake.Calls.SkipWhile(call => !call.StartsWith("TrySetSound(", StringComparison.Ordinal)));
        Assert.DoesNotContain(fake.Calls, call => call.StartsWith("WriteConsoleLine(", StringComparison.Ordinal));
    }

    [Fact]
    public void A_signal_set_to_none_is_silenced()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(done: "\"none\""), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("TrySetSound(Done,None)", fake.Calls);
        Assert.Contains("TrySetSound(Ready,BuiltIn)", fake.Calls);
        Assert.Contains("TrySetSound(Corner,BuiltIn)", fake.Calls);
        Assert.Empty(Balloons(fake));
    }

    [Fact]
    public void A_signal_pointed_at_a_wav_path_is_set_from_that_file()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(ready: "\"click.wav\""), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("TrySetSound(Ready,File(click.wav))", fake.Calls);
        Assert.Empty(Balloons(fake));
    }

    [Fact]
    public void Each_missing_field_is_reported_once_and_unknown_keys_are_ignored()
    {
        var fake = new FakePlatform { ConfigText = "{ \"hotKey\": \"Ctrl+Shift+F5\", \"sounds\": { \"ready\": null, \"corner\": null } }", Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        string[] expectedBalloons =
        [
            "ShowBalloon(ConsoleSnippingTool: config.json: hotkey is missing; using default Ctrl+Win+Z)",
            "ShowBalloon(ConsoleSnippingTool: config.json: armedTimeoutSeconds is missing; using default 30)",
            "ShowBalloon(ConsoleSnippingTool: config.json: sounds.sent is missing; using default built-in click)",
            "ShowBalloon(ConsoleSnippingTool: config.json: sounds.done is missing; using default built-in click)",
            "ShowBalloon(ConsoleSnippingTool: config.json: openAi is missing; using defaults)",
            "ShowBalloon(ConsoleSnippingTool: config.json: dictation is missing; using defaults)",
        ];
        Assert.Equal(expectedBalloons, Balloons(fake));
        Assert.Contains("RegisterHotkey(Ctrl+Win+Z)", fake.Calls);
    }

    [Fact]
    public void An_empty_object_reports_the_five_top_level_fields_in_file_order()
    {
        var (config, problems) = Config.Load("{}");

        string[] expected =
        [
            "config.json: hotkey is missing; using default Ctrl+Win+Z",
            "config.json: armedTimeoutSeconds is missing; using default 30",
            "config.json: sounds is missing; using default built-in click",
            "config.json: openAi is missing; using defaults",
            "config.json: dictation is missing; using defaults",
        ];
        Assert.Equal(expected, problems);
        Assert.Equal(new Config(), config);
    }

    [Fact]
    public void Empty_openAi_and_dictation_objects_report_each_field_missing_with_its_default_in_file_order()
    {
        var json = """{ "hotkey": "Ctrl+Win+Z", "armedTimeoutSeconds": 30, "sounds": { "ready": null, "corner": null, "sent": null, "done": null }, "openAi": {}, "dictation": {} }""";

        var (config, problems) = Config.Load(json);

        string[] expected =
        [
            "config.json: openAi.apiKey is missing; using default null",
            "config.json: dictation.hotkey is missing; using default Ctrl+Win+X",
            "config.json: dictation.listeningTimeoutSeconds is missing; using default 120",
            "config.json: dictation.playbackHotkey is missing; using default Ctrl+Win+A",
            "config.json: dictation.playbackTimeoutSeconds is missing; using default 600",
            "config.json: dictation.insert is missing; using default false",
            "config.json: dictation.model is missing; using default gpt-transcribe",
            "config.json: dictation.languages is missing; using default en, ru",
            "config.json: dictation.prompt is missing; using default null",
            "config.json: dictation.cleanup is missing; using default false",
            "config.json: dictation.cleanupModel is missing; using default gpt-6-luna",
        ];
        Assert.Equal(expected, problems);
        Assert.Equal(new Config(), config);
    }

    [Fact]
    public void Valid_openAi_and_Dictation_values_are_honoured_without_problems()
    {
        var json = ConfigJson.With(
            apiKey: "\"sk-test\"",
            dictationHotkey: "ctrl + alt + d",
            listeningTimeoutSeconds: "45",
            playbackHotkey: "ctrl + alt + m",
            playbackTimeoutSeconds: "300",
            insert: "true",
            model: "\"gpt-transcribe-2\"",
            languages: "[\"en\", \"de\"]",
            prompt: "\"ACME, Kubernetes\"",
            cleanup: "true",
            cleanupModel: "\"gpt-7-mini\"");

        var (config, problems) = Config.Load(json);

        Assert.Empty(problems);
        var expected = new Config
        {
            OpenAiApiKey = "sk-test",
            DictationHotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44, "Ctrl+Alt+D"),
            ListeningTimeoutSeconds = 45,
            PlaybackHotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4D, "Ctrl+Alt+M"),
            PlaybackTimeoutSeconds = 300,
            Insert = true,
            TranscriptionModel = "gpt-transcribe-2",
            Languages = new LanguageList("en", "de"),
            Prompt = "ACME, Kubernetes",
            Cleanup = true,
            CleanupModel = "gpt-7-mini",
        };
        Assert.Equal(expected, config);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Dictation_insert_true_or_false_is_honoured(string insert, bool expected)
    {
        var (config, problems) = Config.Load(ConfigJson.With(insert: insert));

        Assert.Empty(problems);
        Assert.Equal(expected, config.Insert);
    }

    [Theory]
    [InlineData("[\"en\", \"de\"]", "en", "de")]
    [InlineData("[\"ru\"]", "ru")]
    public void Dictation_languages_are_honoured_as_listed(string languages, params string[] expected)
    {
        var (config, problems) = Config.Load(ConfigJson.With(languages: languages));

        Assert.Empty(problems);
        Assert.Equal(new LanguageList(expected), config.Languages);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void A_null_or_empty_language_list_means_no_hint_and_is_not_a_problem(string languages)
    {
        var (config, problems) = Config.Load(ConfigJson.With(languages: languages));

        Assert.Empty(problems);
        Assert.Equal(LanguageList.None, config.Languages);
        Assert.Empty(config.Languages);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("600")]
    public void A_Listening_timeout_at_either_end_of_1_to_600_is_honoured(string seconds)
    {
        var (config, problems) = Config.Load(ConfigJson.With(listeningTimeoutSeconds: seconds));

        Assert.Empty(problems);
        Assert.Equal(int.Parse(seconds), config.ListeningTimeoutSeconds);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("600")]
    public void A_Playback_timeout_at_either_end_of_1_to_600_is_honoured(string seconds)
    {
        var (config, problems) = Config.Load(ConfigJson.With(playbackTimeoutSeconds: seconds));

        Assert.Empty(problems);
        Assert.Equal(int.Parse(seconds), config.PlaybackTimeoutSeconds);
    }

    /// One wrong value per file, with the one problem it must produce; the rest of the file stays at the defaults.
    public static TheoryData<string, string> BadOpenAiAndDictationValues => new()
    {
        { ConfigJson.With(sent: "7"), "config.json: sounds.sent 7 is not null, \"none\" or a .wav path; using default built-in click" },
        { ConfigJson.With(apiKey: "123"), "config.json: openAi.apiKey 123 is not null or a non-empty string; using default null" },
        { ConfigJson.With(apiKey: "\"\""), "config.json: openAi.apiKey \"\" is not null or a non-empty string; using default null" },
        { ConfigJson.With(dictationHotkey: "Ctrl+Home"), "config.json: dictation.hotkey \"Ctrl+Home\" is not a valid Hotkey; using default Ctrl+Win+X" },
        { ConfigJson.With(listeningTimeoutSeconds: "0"), "config.json: dictation.listeningTimeoutSeconds 0 is not an integer from 1 to 600; using default 120" },
        { ConfigJson.With(listeningTimeoutSeconds: "601"), "config.json: dictation.listeningTimeoutSeconds 601 is not an integer from 1 to 600; using default 120" },
        { ConfigJson.With(listeningTimeoutSeconds: "\"long\""), "config.json: dictation.listeningTimeoutSeconds \"long\" is not an integer from 1 to 600; using default 120" },
        { ConfigJson.With(playbackHotkey: "Ctrl+Home"), "config.json: dictation.playbackHotkey \"Ctrl+Home\" is not a valid Hotkey; using default Ctrl+Win+A" },
        { ConfigJson.With(playbackHotkey: "A"), "config.json: dictation.playbackHotkey \"A\" is not a valid Hotkey; using default Ctrl+Win+A" },
        { ConfigJson.With(playbackTimeoutSeconds: "0"), "config.json: dictation.playbackTimeoutSeconds 0 is not an integer from 1 to 600; using default 600" },
        { ConfigJson.With(playbackTimeoutSeconds: "601"), "config.json: dictation.playbackTimeoutSeconds 601 is not an integer from 1 to 600; using default 600" },
        { ConfigJson.With(playbackTimeoutSeconds: "12.5"), "config.json: dictation.playbackTimeoutSeconds 12.5 is not an integer from 1 to 600; using default 600" },
        { ConfigJson.With(playbackTimeoutSeconds: "\"long\""), "config.json: dictation.playbackTimeoutSeconds \"long\" is not an integer from 1 to 600; using default 600" },
        { ConfigJson.With(insert: "\"yes\""), "config.json: dictation.insert \"yes\" is not true or false; using default false" },
        { ConfigJson.With(model: "\"\""), "config.json: dictation.model \"\" is not a non-empty string; using default gpt-transcribe" },
        { ConfigJson.With(model: "5"), "config.json: dictation.model 5 is not a non-empty string; using default gpt-transcribe" },
        { ConfigJson.With(languages: "\"en\""), "config.json: dictation.languages \"en\" is not null or an array of non-empty strings; using default en, ru" },
        { ConfigJson.With(languages: "[\"en\", 5]"), "config.json: dictation.languages [\"en\", 5] is not null or an array of non-empty strings; using default en, ru" },
        { ConfigJson.With(languages: "[\"en\", \"\"]"), "config.json: dictation.languages [\"en\", \"\"] is not null or an array of non-empty strings; using default en, ru" },
        { ConfigJson.With(prompt: "5"), "config.json: dictation.prompt 5 is not null or a string; using default null" },
        { ConfigJson.With(cleanup: "\"yes\""), "config.json: dictation.cleanup \"yes\" is not true or false; using default false" },
        { ConfigJson.With(cleanupModel: "\"\""), "config.json: dictation.cleanupModel \"\" is not a non-empty string; using default gpt-6-luna" },
    };

    [Theory]
    [MemberData(nameof(BadOpenAiAndDictationValues))]
    public void A_bad_openAi_or_Dictation_value_falls_back_to_its_default_with_one_problem_naming_the_field(string json, string problem)
    {
        var (config, problems) = Config.Load(json);

        Assert.Equal([problem], problems);
        Assert.Equal(new Config(), config);
    }

    [Fact]
    public void An_empty_prompt_is_passed_through_as_it_is()
    {
        var (config, problems) = Config.Load(ConfigJson.With(prompt: "\"\""));

        Assert.Empty(problems);
        Assert.Equal("", config.Prompt);
    }

    [Fact]
    public void An_openAi_value_that_is_not_an_object_is_reported_once_for_the_whole_field()
    {
        var json = """{ "hotkey": "Ctrl+Win+Z", "armedTimeoutSeconds": 30, "sounds": { "ready": null, "corner": null, "sent": null, "done": null }, "openAi": "sk-test", "dictation": { "hotkey": "Ctrl+Win+X", "listeningTimeoutSeconds": 120, "playbackHotkey": "Ctrl+Win+A", "playbackTimeoutSeconds": 600, "insert": false, "model": "gpt-transcribe", "languages": ["en", "ru"], "prompt": null, "cleanup": false, "cleanupModel": "gpt-6-luna" } }""";

        var (config, problems) = Config.Load(json);

        Assert.Equal(["config.json: openAi \"sk-test\" is not an object; using defaults"], problems);
        Assert.Equal(new Config(), config);
    }

    [Fact]
    public void A_dictation_value_that_is_an_array_is_reported_once_and_every_Dictation_field_takes_its_default()
    {
        var json = """{ "hotkey": "Ctrl+Win+Z", "armedTimeoutSeconds": 30, "sounds": { "ready": null, "corner": null, "sent": null, "done": null }, "openAi": { "apiKey": null }, "dictation": [1, 2] }""";

        var (config, problems) = Config.Load(json);

        Assert.Equal(["config.json: dictation [1, 2] is not an object; using defaults"], problems);
        Assert.Equal(new Config(), config);
    }

    [Fact]
    public void Identical_Hotkeys_are_reported_once_and_the_Dictation_one_falls_back_to_its_default()
    {
        var (config, problems) = Config.Load(ConfigJson.With(hotkey: "Ctrl+Alt+D", dictationHotkey: "ctrl+alt+d"));

        Assert.Equal(["config.json: dictation.hotkey Ctrl+Alt+D is the same as the Capture Hotkey in effect; using default Ctrl+Win+X"], problems);
        Assert.Equal("Ctrl+Alt+D", config.Hotkey.Text);
        Assert.Equal(Hotkey.DictationDefault, config.DictationHotkey);
    }

    [Fact]
    public void The_identical_Hotkey_rule_compares_against_the_Capture_Hotkey_in_effect_so_a_hotkey_fallen_back_to_its_default_still_clashes()
    {
        var (config, problems) = Config.Load(ConfigJson.With(hotkey: "Ctrl+Home", dictationHotkey: "Ctrl+Win+Z"));

        string[] expected =
        [
            "config.json: hotkey \"Ctrl+Home\" is not a valid Hotkey; using default Ctrl+Win+Z",
            "config.json: dictation.hotkey Ctrl+Win+Z is the same as the Capture Hotkey in effect; using default Ctrl+Win+X",
        ];
        Assert.Equal(expected, problems);
        Assert.Equal(Hotkey.Default, config.Hotkey);
        Assert.Equal(Hotkey.DictationDefault, config.DictationHotkey);
    }

    [Fact]
    public void A_Sent_signal_set_to_none_is_silenced()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(sent: "\"none\""), Environment = ApiKey.InEnvironment };
        var core = new SnipperCore(fake);

        core.Start();

        Assert.Contains("TrySetSound(Sent,None)", fake.Calls);
        Assert.Contains("TrySetSound(Done,BuiltIn)", fake.Calls);
        Assert.Empty(Balloons(fake));
    }

    [Fact]
    public void A_sounds_value_that_is_not_an_object_is_reported_once_for_the_whole_field()
    {
        var json = """{ "hotkey": "Ctrl+Win+Z", "armedTimeoutSeconds": 30, "sounds": "none", "openAi": { "apiKey": null }, "dictation": { "hotkey": "Ctrl+Win+X", "listeningTimeoutSeconds": 120, "playbackHotkey": "Ctrl+Win+A", "playbackTimeoutSeconds": 600, "insert": false, "model": "gpt-transcribe", "languages": ["en", "ru"], "prompt": null, "cleanup": false, "cleanupModel": "gpt-6-luna" } }""";

        var (config, problems) = Config.Load(json);

        Assert.Equal(["config.json: sounds \"none\" is not an object; using default built-in click"], problems);
        Assert.Equal(new Config(), config);
    }

    [Fact]
    public void A_default_Config_serialises_to_the_default_file_the_spec_shows()
    {
        Assert.Equal(Normalise(ConfigJson.With()), Normalise(new Config().ToJson()));
    }

    [Fact]
    public void The_default_file_loads_back_as_the_default_config_without_problems()
    {
        var (config, problems) = Config.Load(new Config().ToJson());

        Assert.Empty(problems);
        Assert.Equal(new Config(), config);
    }

    private static string Normalise(string text) => text.ReplaceLineEndings("\n").TrimEnd();

    [Fact]
    public void No_file_means_defaults_and_nothing_to_report()
    {
        var (config, problems) = Config.Load(null);

        Assert.Empty(problems);
        Assert.Equal(new Config(), config);
    }

    [Fact]
    public void The_Sent_signal_plays_the_SentSound_and_leaves_the_other_signals_alone()
    {
        var config = new Config { SentSound = new SoundSource.File("sent.wav") };

        Assert.Equal(new SoundSource.File("sent.wav"), config.SoundFor(Signal.Sent));
        Assert.Equal(new SoundSource.BuiltIn(), config.SoundFor(Signal.Ready));
        Assert.Equal(new SoundSource.BuiltIn(), config.SoundFor(Signal.Corner));
        Assert.Equal(new SoundSource.BuiltIn(), config.SoundFor(Signal.Done));
    }

    // Hotkey grammar details the Platform seam cannot observe (the fake records only the Text).

    [Theory]
    [InlineData("Ctrl+Shift+F5", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x74, "Ctrl+Shift+F5")]
    [InlineData("win+shift+alt+ctrl+a", HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, 0x41, "Ctrl+Alt+Shift+Win+A")]
    [InlineData("Control+Windows+5", HotkeyModifiers.Control | HotkeyModifiers.Win, 0x35, "Ctrl+Win+5")]
    [InlineData("alt+f24", HotkeyModifiers.Alt, 0x87, "Alt+F24")]
    public void A_valid_Hotkey_parses_to_its_modifiers_VK_code_and_canonical_Text(string text, HotkeyModifiers modifiers, int virtualKey, string canonical)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(modifiers, virtualKey, canonical), hotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+F0")]
    [InlineData("Ctrl+Ctrl+Z")]
    [InlineData("Ctrl++Z")]
    [InlineData("Ctrl+Z+")]
    [InlineData("Ctrl+Esc")]
    [InlineData("Ctrl+ab")]
    public void A_malformed_Hotkey_does_not_parse(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void A_default_Config_holds_the_Dictation_defaults_the_spec_names()
    {
        var config = new Config();

        Assert.Equal("Ctrl+Win+X", config.DictationHotkey.Text);
        Assert.Equal(120, config.ListeningTimeoutSeconds);
        Assert.False(config.Insert);
        Assert.Equal("gpt-transcribe", config.TranscriptionModel);
        Assert.Equal(new LanguageList("en", "ru"), config.Languages);
        Assert.Null(config.Prompt);
        Assert.False(config.Cleanup);
        Assert.Equal("gpt-6-luna", config.CleanupModel);
        Assert.Null(config.OpenAiApiKey);
        Assert.Equal(new SoundSource.BuiltIn(), config.SentSound);
    }

    [Fact]
    public void The_default_Dictation_Hotkey_is_Ctrl_Win_X()
    {
        Assert.Equal(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x58, "Ctrl+Win+X"), Hotkey.DictationDefault);
        Assert.Equal("Ctrl+Win+X", Hotkey.DictationDefault.Text);
    }

    [Fact]
    public void A_default_Config_holds_the_Playback_defaults_the_spec_names()
    {
        var config = new Config();

        Assert.Equal("Ctrl+Win+A", config.PlaybackHotkey.Text);
        Assert.Equal(600, config.PlaybackTimeoutSeconds);
    }

    [Fact]
    public void The_default_Playback_Hotkey_is_Ctrl_Win_A()
    {
        Assert.Equal(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x41, "Ctrl+Win+A"), Hotkey.PlaybackDefault);
        Assert.True(Hotkey.TryParse("Ctrl+Win+A", out var parsed));
        Assert.Equal(parsed, Hotkey.PlaybackDefault);
    }

    private static List<string> Balloons(FakePlatform fake) =>
        fake.Calls.Where(call => call.StartsWith("ShowBalloon(", StringComparison.Ordinal)).ToList();
}
