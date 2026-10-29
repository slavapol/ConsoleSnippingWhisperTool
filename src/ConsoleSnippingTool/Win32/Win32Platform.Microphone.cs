using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// The format the upload transport takes (spec: Microphone); a streaming transport would ask for 24 kHz.
    private const int MicrophoneSampleRate = 16000;

    /// The capture that is open while Listening; null otherwise, so no device is held between Dictations and Windows
    /// shows no microphone indicator while Idle.
    private WasapiCapture? _microphone;

    public bool TryOpenMicrophone()
    {
        // The core opens once per Listening; closing a stray capture first costs nothing and never leaves a device held.
        _microphone?.Close();
        _microphone = WasapiCapture.TryOpen(MicrophoneSampleRate);
        return _microphone is not null;
    }

    public AudioClip CloseMicrophone()
    {
        var capture = _microphone;
        _microphone = null;
        return new AudioClip(capture?.Close() ?? [], MicrophoneSampleRate);
    }
}
