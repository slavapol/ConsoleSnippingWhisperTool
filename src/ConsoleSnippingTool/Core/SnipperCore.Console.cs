namespace ConsoleSnippingTool.Core;

public sealed partial class SnipperCore
{
    public void OnShow()
    {
        if (ConsoleShown)
        {
            return;
        }

        ConsoleShown = true;
        _platform.ShowConsole();
        _platform.WriteConsoleLine(ToolName);
        _platform.WriteConsoleLine($"Press {_config.Hotkey.Text} to capture");
        _platform.WriteConsoleLine($"Hold {_config.MicrophoneHotkey.Text} to dictate");
        _platform.WriteConsoleLine($"Press {_config.PlaybackHotkey.Text} to transcribe playback, again to send");
        _platform.WriteConsoleLine("Esc or right-click cancels a capture; Esc discards a microphone dictation");
        _platform.WriteConsoleLine("");
        foreach (var line in _log)
        {
            _platform.WriteConsoleLine(line);
        }

        _platform.SetTrayMenuState(showEnabled: false, hideEnabled: true);
    }

    public void OnHide()
    {
        if (!ConsoleShown)
        {
            return;
        }

        ConsoleShown = false;
        _platform.HideConsole();
        _platform.SetTrayMenuState(showEnabled: true, hideEnabled: false);
    }
}
