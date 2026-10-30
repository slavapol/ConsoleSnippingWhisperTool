namespace ConsoleSnippingTool.Core;

public sealed partial class SnipperCore
{
    public void Start()
    {
        AcquireInstance();
        LoadConfig();
        ResolveApiKey();
        SetUpSounds();
        RegisterHotkey();
        RegisterDictationHotkey();
        SetUpTray();
    }

    private const string ApiKeyVariable = "OPENAI_API_KEY";

    /// openAi.apiKey when the file sets it, else OPENAI_API_KEY; a blank value counts as no key.
    private void ResolveApiKey()
    {
        var key = _config.OpenAiApiKey ?? _platform.ReadEnvironmentVariable(ApiKeyVariable);
        _apiKey = string.IsNullOrWhiteSpace(key) ? null : key;
        if (_apiKey is null)
        {
            Report(NoApiKeyMessage);
        }
    }

    private static readonly TimeSpan PreviousInstanceExitTimeout = TimeSpan.FromSeconds(5);

    private void AcquireInstance()
    {
        if (_platform.TryAcquireInstance())
        {
            return;
        }

        _platform.SignalPreviousInstance();
        if (!_platform.WaitForPreviousInstanceExit(PreviousInstanceExitTimeout))
        {
            _platform.TerminatePreviousInstance();
        }

        _platform.TryAcquireInstance();
        Log("Replaced previous instance");
    }

    private void LoadConfig()
    {
        var text = _platform.ReadConfigText();
        if (text is null)
        {
            try
            {
                _platform.WriteConfigText(new Config().ToJson());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A read-only exe folder is not a reason to refuse to start (spec: Configuration).
                Report($"{Config.FileName} could not be written: {e.Message.TrimEnd('.')}; continuing with defaults");
            }
        }

        var (config, problems) = Config.Load(text);
        _config = config;
        foreach (var problem in problems)
        {
            Report(problem);
        }
    }

    private void SetUpSounds()
    {
        foreach (var signal in Enum.GetValues<Signal>())
        {
            var source = _config.SoundFor(signal);
            if (_platform.TrySetSound(signal, source))
            {
                continue;
            }

            // A File is named by its path, the one thing the user can fix; the others by their text form.
            var origin = source is SoundSource.File file ? file.Path : source.ToString();
            Log($"Sound for {signal} could not be loaded from {origin}; using built-in click");
            _platform.TrySetSound(signal, new SoundSource.BuiltIn());
        }
    }

    private void RegisterHotkey()
    {
        if (_platform.RegisterHotkey(_config.Hotkey))
        {
            return;
        }

        Report(HotkeyTaken(_config.Hotkey));
    }

    /// A taken Dictation Hotkey leaves Capture working (spec: user story 37).
    private void RegisterDictationHotkey()
    {
        if (_platform.RegisterDictationHotkey(_config.DictationHotkey))
        {
            return;
        }

        Report(HotkeyTaken(_config.DictationHotkey));
    }

    private static string HotkeyTaken(Hotkey hotkey) =>
        $"Hotkey {hotkey.Text} is already taken by another app; edit config.json and relaunch";

    private void SetUpTray()
    {
        _platform.SetTrayTooltip($"{ToolName} (capture {_config.Hotkey.Text}, dictate {_config.DictationHotkey.Text})");
        _platform.SetTrayMenuState(showEnabled: true, hideEnabled: false);
    }

    /// A newer Instance asked this one to exit.
    public void OnReplaceRequested() => OnExit();

    /// Whatever job is under way ends first, so a replaced or closed Instance never leaves the microphone open or
    /// pastes after it is gone (spec: user stories 40 and 41).
    public void OnExit()
    {
        switch (_state)
        {
            case State.Armed:
            case State.FirstCorner:
                Cancel(CancelReason.Exit);
                break;
            case State.Listening:
                Discard(DiscardReason.Exit);
                break;
            case State.Transcribing:
                AbandonTranscription();
                break;
        }

        // No-op while the Console is hidden; lets the adapter detach its console handler before the process goes.
        OnHide();
        _platform.UnregisterHotkey();
        _platform.UnregisterDictationHotkey();
        _platform.UnregisterPlaybackHotkey(); // harmless while Start does not register it yet (Playback Dictation ticket 02)
        _platform.RemoveTrayIcon();
        _platform.Exit();
    }

    /// An exception nothing else caught, handed over by Program from the message loop or another thread: the user is
    /// told and the tool keeps running rather than dying silently.
    public void OnUnhandledException(Exception exception) =>
        Report($"Unhandled error: {exception.GetType().Name}: {exception.Message}");
}
