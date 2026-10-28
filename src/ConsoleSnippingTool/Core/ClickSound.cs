namespace ConsoleSnippingTool.Core;

/// The built-in key click, synthesised in memory so no audio asset ships. The Win32 adapter plays <see cref="Wav"/>
/// for <see cref="SoundSource.BuiltIn"/>.
public static class ClickSound
{
    public const int SampleRate = 44100;
    private const short Channels = 1;
    private const short BitsPerSample = 16;

    /// A complete RIFF/WAVE file: 16-bit mono PCM at 44.1 kHz.
    public static byte[] Wav()
    {
        var samples = Samples();
        var dataSize = samples.Length * sizeof(short);
        const short blockAlign = Channels * BitsPerSample / 8;

        using var stream = new MemoryStream(44 + dataSize);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);

            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write(Channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * blockAlign);
            writer.Write(blockAlign);
            writer.Write(BitsPerSample);

            writer.Write("data"u8);
            writer.Write(dataSize);
            foreach (var sample in samples)
            {
                writer.Write(sample);
            }
        }

        return stream.ToArray();
    }

    // A key-like click: white noise under a fast exponential decay, loud enough to notice and gone before it annoys.
    private const double DurationSeconds = 0.030;
    private const double DecaySeconds = 0.006; // envelope time constant; e^-5 by the end
    private const double Peak = 0.6; // of full scale
    private const uint Seed = 0x5EED_C11C; // any non-zero value; fixed so the bytes never change

    private static short[] Samples()
    {
        var count = (int)(SampleRate * DurationSeconds);
        var shaped = new double[count];
        var state = Seed;
        for (var i = 0; i < count; i++)
        {
            var envelope = Math.Exp(-i / (SampleRate * DecaySeconds));
            shaped[i] = envelope * NextNoise(ref state);
        }

        var scale = Peak * short.MaxValue / shaped.Max(Math.Abs);
        return shaped.Select(value => (short)Math.Round(value * scale)).ToArray();
    }

    // xorshift32, a few lines of our own, so the bytes do not depend on the runtime's Random implementation.
    private static double NextNoise(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state / 2147483648.0 - 1.0; // [0, 2^32) → [-1, 1)
    }
}
