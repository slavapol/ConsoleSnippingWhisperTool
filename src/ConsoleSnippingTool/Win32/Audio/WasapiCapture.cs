using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ConsoleSnippingTool.Win32;

/// One Listening's worth of microphone: the Default Communication Device opened in WASAPI shared mode, the audio engine
/// converting to 16-bit mono PCM at the requested rate, drained by a worker thread into memory until Close. TryOpen
/// runs on the caller's thread (the message loop, STA: Microsoft asks for the first IAudioClient call to be made there);
/// only the drain loop runs on the worker. Nothing survives Close, so every Dictation opens the device afresh and a
/// changed default is picked up next time (spec: Microphone).
internal sealed class WasapiCapture
{
    private const ushort Channels = 1;
    private const ushort BitsPerSample = 16;
    private const ushort BlockAlign = Channels * BitsPerSample / 8;

    /// The engine-side buffer in 100 ns units: half a second, so a GC pause or a busy desktop loses no packet while the
    /// worker is late. The engine still signals the event once per period, about every 10 ms.
    private const long BufferDuration = 5_000_000;

    /// How long the drain loop waits for a packet before re-checking the stop flag. Close also signals the event, so
    /// this only bounds the join when the device has gone quiet.
    private const int WaitMilliseconds = 100;

    private static readonly StrategyBasedComWrappers ComWrappers = new();

    private readonly IAudioClient _client;
    private readonly IAudioCaptureClient _captureClient;
    private readonly AutoResetEvent _packetReady;
    private readonly MemoryStream _pcm16 = new();
    private readonly Thread _drain;
    private volatile bool _stopping;

    private WasapiCapture(IAudioClient client, IAudioCaptureClient captureClient, AutoResetEvent packetReady)
    {
        _client = client;
        _captureClient = captureClient;
        _packetReady = packetReady;
        _drain = new Thread(Drain) { Name = "Microphone drain", IsBackground = true };
        _drain.SetApartmentState(ApartmentState.MTA);
    }

    /// Opens the Default Communication Device at sampleRate and starts capturing. Null when there is no capture
    /// endpoint (E_NOTFOUND), when Windows denies the microphone (E_ACCESSDENIED from Initialize) or when any other
    /// step fails; whatever was created by then is released.
    public static WasapiCapture? TryOpen(int sampleRate)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioClient? client = null;
        IAudioCaptureClient? captureClient = null;
        AutoResetEvent? packetReady = null;
        var opened = false;
        try
        {
            enumerator = CreateEnumerator();
            if (enumerator is null)
            {
                return null;
            }

            if (Failed(enumerator.GetDefaultAudioEndpoint(NativeMethods.eCapture, NativeMethods.eCommunications, out var devicePointer)))
            {
                return null;
            }

            device = Wrap<IMMDevice>(devicePointer);

            var iid = typeof(IAudioClient).GUID;
            if (Failed(device.Activate(in iid, NativeMethods.CLSCTX_ALL, 0, out var clientPointer)))
            {
                return null;
            }

            client = Wrap<IAudioClient>(clientPointer);

            // AUTOCONVERTPCM makes the engine matrix and resample from the device's mix format to ours.
            const uint flags = NativeMethods.AUDCLNT_STREAMFLAGS_EVENTCALLBACK
                | NativeMethods.AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM
                | NativeMethods.AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY;
            var format = Pcm16Mono(sampleRate);
            if (Failed(client.Initialize(NativeMethods.AUDCLNT_SHAREMODE_SHARED, flags, BufferDuration, 0, in format, 0)))
            {
                return null;
            }

            packetReady = new AutoResetEvent(initialState: false);
            if (Failed(client.SetEventHandle(packetReady.SafeWaitHandle.DangerousGetHandle())))
            {
                return null;
            }

            iid = typeof(IAudioCaptureClient).GUID;
            if (Failed(client.GetService(in iid, out var capturePointer)))
            {
                return null;
            }

            captureClient = Wrap<IAudioCaptureClient>(capturePointer);

            if (Failed(client.Start()))
            {
                return null;
            }

            var capture = new WasapiCapture(client, captureClient, packetReady);
            capture._drain.Start();
            opened = true;
            return capture;
        }
        finally
        {
            // The stream holds what it needs; the device and the enumerator are only the way to it.
            Release(device);
            Release(enumerator);
            if (!opened)
            {
                packetReady?.Dispose();
                Release(captureClient);
                Release(client);
            }
        }
    }

    /// Stops the stream, joins the drain thread, releases every COM object and the event, and returns the samples:
    /// 16-bit little-endian mono PCM at the rate the capture was opened with.
    public byte[] Close()
    {
        _stopping = true;
        _packetReady.Set();
        _ = _client.Stop(); // a device that vanished mid-Listening fails here; the samples so far still count
        _drain.Join();
        Release(_captureClient);
        Release(_client);
        _packetReady.Dispose();
        return _pcm16.ToArray();
    }

    private void Drain()
    {
        var packet = Array.Empty<byte>();
        while (!_stopping)
        {
            _packetReady.WaitOne(WaitMilliseconds);
            if (!DrainPackets(ref packet))
            {
                return; // the device went away (AUDCLNT_E_DEVICE_INVALIDATED); Close returns what was captured
            }
        }
    }

    /// Copies every packet the engine has ready into the stream; false on the first failing call.
    private bool DrainPackets(ref byte[] packet)
    {
        while (true)
        {
            if (Failed(_captureClient.GetNextPacketSize(out var frames)))
            {
                return false;
            }

            if (frames == 0)
            {
                return true;
            }

            if (Failed(_captureClient.GetBuffer(out var data, out frames, out var flags, out _, out _)))
            {
                return false;
            }

            var length = (int)(frames * BlockAlign);
            if (packet.Length < length)
            {
                packet = new byte[length];
            }

            if ((flags & NativeMethods.AUDCLNT_BUFFERFLAGS_SILENT) != 0)
            {
                Array.Clear(packet, 0, length); // the engine reports silence and may leave the buffer stale
            }
            else
            {
                Marshal.Copy(data, packet, 0, length);
            }

            _pcm16.Write(packet, 0, length);

            if (Failed(_captureClient.ReleaseBuffer(frames)))
            {
                return false;
            }
        }
    }

    private static IMMDeviceEnumerator? CreateEnumerator()
    {
        var iid = typeof(IMMDeviceEnumerator).GUID;
        var hresult = NativeMethods.CoCreateInstance(in NativeMethods.CLSID_MMDeviceEnumerator, 0, NativeMethods.CLSCTX_ALL, in iid, out var pointer);
        return Failed(hresult) ? null : Wrap<IMMDeviceEnumerator>(pointer);
    }

    /// Wraps a COM pointer we own as a unique instance, which is what makes Release deterministic, and gives up our own
    /// reference: the wrapper has taken its own.
    private static T Wrap<T>(nint pointer) where T : class
    {
        try
        {
            return (T)ComWrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private static void Release(object? comObject) => (comObject as ComObject)?.FinalRelease();

    private static bool Failed(int hresult) => hresult < 0;

    private static WaveFormatEx Pcm16Mono(int sampleRate) => new()
    {
        FormatTag = NativeMethods.WAVE_FORMAT_PCM,
        Channels = Channels,
        SamplesPerSec = (uint)sampleRate,
        AvgBytesPerSec = (uint)(sampleRate * BlockAlign),
        BlockAlign = BlockAlign,
        BitsPerSample = BitsPerSample,
        Size = 0,
    };
}
