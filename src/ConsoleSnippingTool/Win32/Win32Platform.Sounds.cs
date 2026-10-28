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
                player = LoadPlayer(ClickSound.Wav());
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
}
