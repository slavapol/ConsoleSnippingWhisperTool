using System.Buffers.Binary;
using System.Text;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

// Wav.FromPcm16 is pure, I/O-free code below the Platform seam, tested directly: the in-memory RIFF/WAVE writer the
// built-in click and the microphone clip share. Expected header values are the canonical 44-byte PCM layout worked out
// by hand, not recomputed the way the writer does.
public class WavTests
{
    [Fact]
    public void Four_16_kHz_mono_samples_get_a_44_byte_header_whose_sizes_and_fmt_fields_describe_them()
    {
        byte[] pcm16 = [0x01, 0x00, 0xFF, 0xFF, 0x00, 0x80, 0xFF, 0x7F];

        var bytes = Wav.FromPcm16(pcm16, 16000);

        Assert.Equal(52, bytes.Length);
        Assert.Equal("RIFF", Ascii(bytes, 0));
        Assert.Equal(44, Int32At(bytes, 4)); // file length minus the 8-byte RIFF header
        Assert.Equal("WAVE", Ascii(bytes, 8));
        Assert.Equal("fmt ", Ascii(bytes, 12));
        Assert.Equal(16, Int32At(bytes, 16));
        Assert.Equal(1, Int16At(bytes, 20)); // WAVE_FORMAT_PCM
        Assert.Equal(1, Int16At(bytes, 22)); // channels
        Assert.Equal(16000, Int32At(bytes, 24)); // sample rate
        Assert.Equal(32000, Int32At(bytes, 28)); // byte rate
        Assert.Equal(2, Int16At(bytes, 32)); // block align
        Assert.Equal(16, Int16At(bytes, 34)); // bits per sample
        Assert.Equal("data", Ascii(bytes, 36));
        Assert.Equal(8, Int32At(bytes, 40));
        Assert.Equal(pcm16, bytes[44..]);
    }

    [Fact]
    public void An_empty_buffer_gives_a_44_byte_file_with_a_36_byte_RIFF_body_and_no_data()
    {
        var bytes = Wav.FromPcm16([], 16000);

        Assert.Equal(44, bytes.Length);
        Assert.Equal(36, Int32At(bytes, 4));
        Assert.Equal(0, Int32At(bytes, 40));
    }

    // The click is 30 ms at 44.1 kHz: 1323 samples, so 44 + 2646 bytes. Pinned so the shared writer cannot change it.
    [Fact]
    public void The_click_still_starts_with_RIFF_is_44_bytes_plus_two_per_sample_and_declares_44100_Hz_mono_16_bit()
    {
        var bytes = ClickSound.Wav();

        Assert.Equal("RIFF", Ascii(bytes, 0));
        Assert.Equal(44 + 1323 * 2, bytes.Length);
        Assert.Equal(1323 * 2, Int32At(bytes, 40));
        Assert.Equal(1, Int16At(bytes, 22));
        Assert.Equal(44100, Int32At(bytes, 24));
        Assert.Equal(16, Int16At(bytes, 34));
    }

    private static string Ascii(byte[] bytes, int offset) => Encoding.ASCII.GetString(bytes, offset, 4);

    private static int Int32At(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));

    private static short Int16At(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
}
