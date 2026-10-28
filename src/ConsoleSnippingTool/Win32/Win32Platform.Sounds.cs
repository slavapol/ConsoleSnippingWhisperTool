using System.Media;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// One player per Signal, indexed by its enum value; null plays nothing.
    private readonly SoundPlayer?[] _players = new SoundPlayer?[3];

    public bool TrySetSound(Signal signal, SoundSource source)
    {
        SoundPlayer? player;
        switch (source)
        {
            case SoundSource.None:
                player = null;
                break;
            case SoundSource.BuiltIn:
                player = LoadPlayer(SilentWav());
                break;
            case SoundSource.File file:
                try
                {
                    var bytes = File.ReadAllBytes(Path.Combine(ExeFolder, file.Path));
                    if (!IsWave(bytes))
                    {
                        return false;
                    }

                    player = LoadPlayer(bytes);
                }
                catch (Exception)
                {
                    return false;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, null);
        }

        _players[(int)signal]?.Dispose();
        _players[(int)signal] = player;
        return true;
    }

    public void PlaySound(Signal signal)
    {
        try
        {
            _players[(int)signal]?.Play();
        }
        catch (InvalidOperationException)
        {
            // SoundPlayer validates the WAV body only when playing; a file with an unsupported encoding stays silent.
        }
    }

    private static SoundPlayer LoadPlayer(byte[] wav)
    {
        var player = new SoundPlayer(new MemoryStream(wav));
        player.Load();
        return player;
    }

    /// The RIFF/WAVE signature SoundPlayer itself checks, done at load time so a non-WAV file reports as not loaded.
    private static bool IsWave(byte[] bytes) =>
        bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8);

    // TODO(08): replace with ClickSound.Wav()
    /// 30 ms of silence as a 16-bit mono 44.1 kHz PCM WAV; stands in for the synthesised click of ticket 06.
    private static byte[] SilentWav()
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int milliseconds = 30;
        const short blockAlign = channels * bitsPerSample / 8;
        const int dataBytes = sampleRate * milliseconds / 1000 * blockAlign;

        using var stream = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);                       // fmt chunk size
        writer.Write((short)1);                 // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);  // byte rate
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        writer.Write(new byte[dataBytes]);
        writer.Flush();
        return stream.ToArray();
    }
}
