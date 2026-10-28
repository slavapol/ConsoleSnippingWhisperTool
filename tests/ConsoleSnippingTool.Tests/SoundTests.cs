using System.Buffers.Binary;
using System.Text;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// Two seams: the bytes ClickSound.Wav() returns (parsed as a canonical 44-byte-header PCM WAV), and the sound step
// of SnipperCore.Start() observed through FakePlatform's Calls.
public class SoundTests
{
    [Fact]
    public void The_click_is_a_well_formed_RIFF_WAVE_file_with_a_fmt_chunk_and_a_data_chunk_of_matching_length()
    {
        var bytes = ClickSound.Wav();

        var wav = Parse(bytes);
        Assert.Equal("RIFF", wav.RiffTag);
        Assert.Equal(bytes.Length - 8, wav.RiffSize);
        Assert.Equal("WAVE", wav.WaveTag);
        Assert.Equal("fmt ", wav.FmtTag);
        Assert.Equal(16, wav.FmtSize);
        Assert.Equal("data", wav.DataTag);
        Assert.Equal(bytes.Length - HeaderLength, wav.DataSize);
    }

    [Fact]
    public void The_click_is_16_bit_mono_PCM_at_44100_Hz()
    {
        var wav = Parse(ClickSound.Wav());

        Assert.Equal(1, wav.Format);
        Assert.Equal(1, wav.Channels);
        Assert.Equal(44100, wav.SampleRate);
        Assert.Equal(16, wav.BitsPerSample);
        Assert.Equal(2, wav.BlockAlign);
        Assert.Equal(88200, wav.ByteRate);
    }

    [Fact]
    public void The_click_lasts_between_20_and_40_ms()
    {
        var wav = Parse(ClickSound.Wav());

        var milliseconds = 1000.0 * wav.Samples.Length / 44100;
        Assert.InRange(milliseconds, 20, 40);
    }

    [Fact]
    public void The_click_decays_so_that_the_last_quarter_is_below_10_percent_of_the_first_quarter_by_RMS()
    {
        var wav = Parse(ClickSound.Wav());

        var quarter = wav.Samples.Length / 4;
        var first = Rms(wav.Samples.Take(quarter));
        var last = Rms(wav.Samples.Skip(wav.Samples.Length - quarter));
        Assert.True(last < 0.1 * first, $"last-quarter RMS {last:F1} is not below 10% of first-quarter RMS {first:F1}");
    }

    [Fact]
    public void The_click_peaks_at_about_0_6_of_full_scale()
    {
        var wav = Parse(ClickSound.Wav());

        var peak = wav.Samples.Max(sample => Math.Abs((int)sample)) / 32767.0;
        Assert.InRange(peak, 0.55, 0.65);
    }

    [Fact]
    public void The_click_is_the_same_bytes_every_time()
    {
        Assert.Equal(ClickSound.Wav(), ClickSound.Wav());
    }

    [Fact]
    public void A_File_source_that_fails_to_load_falls_back_to_the_built_in_click_for_that_signal_only()
    {
        var fake = new FakePlatform
        {
            ConfigText = ConfigJson.With(done: "\"bad.wav\""),
            SoundLoadResults = { [Signal.Done] = false },
        };
        var core = new SnipperCore(fake);

        core.Start();

        string[] expected =
        [
            "TrySetSound(Ready,BuiltIn)",
            "TrySetSound(Corner,BuiltIn)",
            "TrySetSound(Done,File(bad.wav))",
            "TrySetSound(Done,BuiltIn)",
        ];
        Assert.Equal(expected, SoundCalls(fake));
    }

    [Fact]
    public void A_File_source_that_fails_to_load_is_reported_in_the_Console_as_a_fallback_to_the_built_in_click()
    {
        var fake = new FakePlatform
        {
            ConfigText = ConfigJson.With(done: "\"bad.wav\""),
            SoundLoadResults = { [Signal.Done] = false },
        };
        var core = new SnipperCore(fake);

        core.Start();
        core.OnShow();

        Assert.Contains(fake.Calls, call =>
            call.StartsWith("WriteConsoleLine(", StringComparison.Ordinal)
            && call.Contains("Sound for Done could not be loaded from bad.wav; using built-in click", StringComparison.Ordinal));
    }

    [Fact]
    public void A_File_source_that_loads_is_set_once_and_nothing_is_reported()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(ready: "\"click.wav\"") };
        var core = new SnipperCore(fake);

        core.Start();
        core.OnShow();

        string[] expected =
        [
            "TrySetSound(Ready,File(click.wav))",
            "TrySetSound(Corner,BuiltIn)",
            "TrySetSound(Done,BuiltIn)",
        ];
        Assert.Equal(expected, SoundCalls(fake));
        Assert.DoesNotContain(fake.Calls, call => call.Contains("Sound for", StringComparison.Ordinal));
    }

    [Fact]
    public void A_None_source_is_set_once()
    {
        var fake = new FakePlatform { ConfigText = ConfigJson.With(corner: "\"none\"") };
        var core = new SnipperCore(fake);

        core.Start();

        string[] expected =
        [
            "TrySetSound(Ready,BuiltIn)",
            "TrySetSound(Corner,None)",
            "TrySetSound(Done,BuiltIn)",
        ];
        Assert.Equal(expected, SoundCalls(fake));
    }

    private static IEnumerable<string> SoundCalls(FakePlatform fake) =>
        fake.Calls.Where(call => call.StartsWith("TrySetSound(", StringComparison.Ordinal));

    private static double Rms(IEnumerable<short> samples) =>
        Math.Sqrt(samples.Average(sample => (double)sample * sample));

    // A canonical PCM WAV: 44-byte header, then little-endian samples.
    private const int HeaderLength = 44;

    private sealed record Wav(
        string RiffTag,
        int RiffSize,
        string WaveTag,
        string FmtTag,
        int FmtSize,
        int Format,
        int Channels,
        int SampleRate,
        int ByteRate,
        int BlockAlign,
        int BitsPerSample,
        string DataTag,
        int DataSize,
        short[] Samples);

    private static Wav Parse(byte[] bytes)
    {
        var span = bytes.AsSpan();
        var dataSize = BinaryPrimitives.ReadInt32LittleEndian(span[40..]);
        var samples = new short[dataSize / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(span[(HeaderLength + 2 * i)..]);
        }

        return new Wav(
            RiffTag: Encoding.ASCII.GetString(span[..4]),
            RiffSize: BinaryPrimitives.ReadInt32LittleEndian(span[4..]),
            WaveTag: Encoding.ASCII.GetString(span[8..12]),
            FmtTag: Encoding.ASCII.GetString(span[12..16]),
            FmtSize: BinaryPrimitives.ReadInt32LittleEndian(span[16..]),
            Format: BinaryPrimitives.ReadInt16LittleEndian(span[20..]),
            Channels: BinaryPrimitives.ReadInt16LittleEndian(span[22..]),
            SampleRate: BinaryPrimitives.ReadInt32LittleEndian(span[24..]),
            ByteRate: BinaryPrimitives.ReadInt32LittleEndian(span[28..]),
            BlockAlign: BinaryPrimitives.ReadInt16LittleEndian(span[32..]),
            BitsPerSample: BinaryPrimitives.ReadInt16LittleEndian(span[34..]),
            DataTag: Encoding.ASCII.GetString(span[36..40]),
            DataSize: dataSize,
            Samples: samples);
    }
}
