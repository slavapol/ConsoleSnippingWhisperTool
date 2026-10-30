using System.Runtime.InteropServices;

namespace ConsoleSnippingTool.Win32;

/// The Core Audio constants and the P/Invokes the capture of both Sources needs beside the COM interfaces in
/// WasapiInterfaces.cs. Values from combaseapi.h, mmdeviceapi.h, audiosessiontypes.h, mmreg.h, audioclient.h,
/// coml2api.h, wtypes.h, functiondiscoverykeys_devpkey.h and propidl.h.
internal static partial class NativeMethods
{
    // CoCreateInstance
    public const uint CLSCTX_ALL = 0x17; // INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER
    public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    // IMMDeviceEnumerator.GetDefaultAudioEndpoint
    public const int eRender = 0; // EDataFlow: the output endpoint Playback records by loopback (ADR-0005)
    public const int eCapture = 1; // EDataFlow: the input endpoint the Microphone records
    public const int eCommunications = 2; // ERole: the Default Communication Device, for both Sources

    // IAudioClient.Initialize
    public const int AUDCLNT_SHAREMODE_SHARED = 0;
    public const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    public const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    public const uint AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;
    public const uint AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = 0x80000000;
    public const ushort WAVE_FORMAT_PCM = 1;

    // IAudioCaptureClient.GetBuffer
    public const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;

    // IMMDevice.OpenPropertyStore and IPropertyStore.GetValue
    public const uint STGM_READ = 0;
    public const ushort VT_LPWSTR = 31;
    public static readonly PropertyKey PKEY_Device_FriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    [DllImport("ole32.dll")]
    public static extern int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);

    /// Frees what a PROPVARIANT owns, here the string of a VT_LPWSTR, and leaves it VT_EMPTY.
    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant pvar);
}
