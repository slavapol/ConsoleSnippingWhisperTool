using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// The format the upload transport takes, for both Sources (spec: Microphone); a streaming transport would ask for
    /// 24 kHz.
    private const int SourceSampleRate = 16000;

    /// The capture that is open while Listening; null otherwise, so no device is held between Dictations and Windows
    /// shows no microphone indicator while Idle.
    private WasapiCapture? _source;

    public bool TryOpenSource(DictationSource source, out string deviceName)
    {
        // The core opens once per Listening; closing a stray capture first costs nothing and never leaves a device held.
        _source?.Close();
        _source = null;

        // The endpoint's friendly name and the Playback loopback capture are ticket 04 of Playback Dictation; until
        // then the name is unknown and Playback does not open.
        deviceName = "";
        if (source != DictationSource.Microphone)
        {
            return false;
        }

        _source = WasapiCapture.TryOpen(SourceSampleRate);
        return _source is not null;
    }

    public AudioClip CloseSource()
    {
        var capture = _source;
        _source = null;
        return new AudioClip(capture?.Close() ?? [], SourceSampleRate);
    }
}
