namespace ConsoleSnippingTool.Core;

public sealed partial class SnipperCore
{
    public void Start()
    {
        AcquireInstance();
        LoadConfig();
        SetUpSounds();
        RegisterHotkey();
        SetUpTray();
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
            _platform.WriteConfigText(new Config().ToJson());
        }

        // Ticket 04 parses non-null text; until then any text yields the defaults.
        _config = new Config();
    }

    private void SetUpSounds()
    {
        foreach (var signal in new[] { Signal.Ready, Signal.Corner, Signal.Done })
        {
            var source = _config.SoundFor(signal);
            if (_platform.TrySetSound(signal, source))
            {
                continue;
            }

            var path = source is SoundSource.File file ? file.Path : source.GetType().Name;
            Log($"Sound for {signal} could not be loaded from {path}; using built-in click");
            _platform.TrySetSound(signal, new SoundSource.BuiltIn());
        }
    }

    private void RegisterHotkey()
    {
        if (_platform.RegisterHotkey(_config.Hotkey))
        {
            return;
        }

        var message = $"Hotkey {_config.Hotkey.Text} is already taken by another app; edit config.json and relaunch";
        _platform.ShowBalloon(ToolName, message);
        Log(message);
    }

    private void SetUpTray()
    {
        _platform.SetTrayTooltip($"{ToolName} ({_config.Hotkey.Text})");
        _platform.SetTrayMenuState(showEnabled: true, hideEnabled: false);
    }

    /// A newer Instance asked this one to exit.
    public void OnReplaceRequested() => OnExit();

    public void OnExit()
    {
        if (_state != State.Idle)
        {
            Cancel("exit");
        }

        // No-op while the Console is hidden; lets the adapter detach its console handler before the process goes.
        OnHide();
        _platform.UnregisterHotkey();
        _platform.RemoveTrayIcon();
        _platform.Exit();
    }
}
