using System.Buffers.Binary;

namespace ConsoleSnippingTool.Core;

/// The RIFF/WAVE container around 16-bit little-endian PCM, written in memory. The built-in click (ClickSound) and the
/// microphone clip the upload transport sends go through this one writer; no audio ever reaches the disk.
public static class Wav
{
    /// The canonical PCM header: RIFF and WAVE tags, a 16-byte fmt chunk and the data chunk header.
    public const int HeaderLength = 44;

    private const short FormatPcm = 1;
    private const short BitsPerSample = 16;

    /// A complete WAV file for the given samples, as a player or the Service would read it.
    public static byte[] FromPcm16(byte[] pcm16, int sampleRate, short channels)
    {
        var blockAlign = (short)(channels * BitsPerSample / 8);
        var bytes = new byte[HeaderLength + pcm16.Length];
        var header = bytes.AsSpan(0, HeaderLength);

        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], HeaderLength - 8 + pcm16.Length);
        "WAVE"u8.CopyTo(header[8..]);

        "fmt "u8.CopyTo(header[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], FormatPcm);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], channels);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], sampleRate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], BitsPerSample);

        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], pcm16.Length);
        pcm16.CopyTo(bytes, HeaderLength);

        return bytes;
    }
}
