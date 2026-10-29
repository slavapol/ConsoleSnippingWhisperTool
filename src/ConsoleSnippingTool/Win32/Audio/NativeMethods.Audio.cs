using System.Runtime.InteropServices;

namespace ConsoleSnippingTool.Win32;

/// The Core Audio constants and the one P/Invoke the microphone capture needs beside the COM interfaces in
/// WasapiInterfaces.cs. Values from combaseapi.h, mmdeviceapi.h, audiosessiontypes.h, mmreg.h and audioclient.h.
internal static partial class NativeMethods
{
    // CoCreateInstance
    public const uint CLSCTX_ALL = 0x17; // INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER
    public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    // IMMDeviceEnumerator.GetDefaultAudioEndpoint
    public const int eCapture = 1; // EDataFlow
    public const int eCommunications = 2; // ERole: the Default Communication Device (spec: Microphone)

    // IAudioClient.Initialize
    public const int AUDCLNT_SHAREMODE_SHARED = 0;
    public const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    public const uint AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;
    public const uint AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = 0x80000000;
    public const ushort WAVE_FORMAT_PCM = 1;

    // IAudioCaptureClient.GetBuffer
    public const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;

    [DllImport("ole32.dll")]
    public static extern int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);
}
