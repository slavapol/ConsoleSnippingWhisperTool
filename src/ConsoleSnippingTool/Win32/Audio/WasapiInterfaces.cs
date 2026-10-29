using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ConsoleSnippingTool.Win32;

// The Core Audio interfaces the microphone capture uses, declared for the COM source generator: methods in vtable order
// as in the Windows SDK headers (mmdeviceapi.h, audioclient.h), the slots we never call declared with plain signatures
// so the table lines up. The methods we do call return their HRESULT ([PreserveSig]) so a failure such as E_NOTFOUND
// or E_ACCESSDENIED is a value WasapiCapture.TryOpen inspects, not an exception. Interface pointers come back as nint
// and WasapiCapture wraps them as unique instances, which is what lets it release them deterministically.

[GeneratedComInterface]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
internal partial interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, uint stateMask, out nint devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out nint endpoint);

    void GetDevice(nint id, out nint device);

    void RegisterEndpointNotificationCallback(nint client);

    void UnregisterEndpointNotificationCallback(nint client);
}

[GeneratedComInterface]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
internal partial interface IMMDevice
{
    [PreserveSig]
    int Activate(in Guid iid, uint clsCtx, nint activationParams, out nint activated);

    void OpenPropertyStore(uint access, out nint properties);

    void GetId(out nint id);

    void GetState(out uint state);
}

[GeneratedComInterface]
[Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
internal partial interface IAudioClient
{
    [PreserveSig]
    int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, in WaveFormatEx format, nint audioSessionGuid);

    void GetBufferSize(out uint bufferFrames);

    void GetStreamLatency(out long latency);

    void GetCurrentPadding(out uint paddingFrames);

    void IsFormatSupported(int shareMode, nint format, out nint closestMatch);

    void GetMixFormat(out nint deviceFormat);

    void GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);

    [PreserveSig]
    int Start();

    [PreserveSig]
    int Stop();

    void Reset();

    [PreserveSig]
    int SetEventHandle(nint eventHandle);

    [PreserveSig]
    int GetService(in Guid iid, out nint service);
}

[GeneratedComInterface]
[Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
internal partial interface IAudioCaptureClient
{
    [PreserveSig]
    int GetBuffer(out nint data, out uint framesToRead, out uint flags, out ulong devicePosition, out ulong qpcPosition);

    [PreserveSig]
    int ReleaseBuffer(uint framesRead);

    [PreserveSig]
    int GetNextPacketSize(out uint framesInNextPacket);
}

/// WAVEFORMATEX (mmreg.h), byte-packed as in the header.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WaveFormatEx
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SamplesPerSec;
    public uint AvgBytesPerSec;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort Size;
}
