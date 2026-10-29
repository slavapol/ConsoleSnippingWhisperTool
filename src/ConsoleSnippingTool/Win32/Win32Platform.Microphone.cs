using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// The format the upload transport takes (spec: Microphone); a streaming transport would ask for 24 kHz.
    private const int MicrophoneSampleRate = 16000;

    // WASAPI capture of the Default Communication Device is ticket 05. Until then there is no microphone: open fails,
    // so the core's "No microphone found" path is what a user meets, and close returns an empty clip.

    public bool TryOpenMicrophone() => false;

    public AudioClip CloseMicrophone() => new([], MicrophoneSampleRate);
}
