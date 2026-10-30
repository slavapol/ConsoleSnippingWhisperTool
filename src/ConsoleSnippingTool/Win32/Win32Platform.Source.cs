using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// The format the upload transport takes, for both Sources (spec: Microphone); a streaming transport would ask for
    /// 24 kHz.
    private const int SourceSampleRate = 16000;

    /// The capture that is open while Listening, of either Source; null otherwise, so no device is held between
    /// Dictations and Windows shows no microphone indicator while Idle.
    private WasapiCapture? _capture;

    /// Opens the Source the core names on the Default Communication Device and reports the endpoint's friendly name,
    /// empty when it could not be read or the open failed.
    public bool TryOpenSource(DictationSource source, out string deviceName)
    {
        // The core opens once per Listening; closing a stray capture first costs nothing and never leaves a device held.
        _capture?.Close();
        _capture = WasapiCapture.TryOpen(source, SourceSampleRate);
        deviceName = _capture?.DeviceName ?? "";
        return _capture is not null;
    }

    public AudioClip CloseSource()
    {
        var capture = _capture;
        _capture = null;
        return new AudioClip(capture?.Close() ?? [], SourceSampleRate);
    }
}
