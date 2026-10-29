using System.Buffers.Binary;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32.OpenAi;

/// The upload transport: after the Release, the whole clip as one WAV in one multipart POST to
/// v1/audio/transcriptions (ADR-0004). The WAV is built here, on the caller's thread pool thread.
internal sealed class UploadTransport : ITranscriptionTransport
{
    public async Task<string> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken cancellationToken)
    {
        var form = OpenAiJson.TranscriptionForm(ToWav(clip), request.Model, request.Language, request.Prompt);
        var body = await OpenAiHttp
            .SendAsync(HttpMethod.Post, "v1/audio/transcriptions", request.ApiKey, form, cancellationToken)
            .ConfigureAwait(false);
        return OpenAiJson.ParseTranscript(body);
    }

    // ticket 08: replace with Wav.FromPcm16 (ticket 05 extracts the header writer from ClickSound; this is the same
    // canonical 44-byte header in front of the samples).
    /// The clip as a RIFF/WAVE file: 16-bit mono PCM at the clip's sample rate.
    private static byte[] ToWav(AudioClip clip)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        const short blockAlign = channels * bitsPerSample / 8;
        var dataSize = clip.Pcm16.Length;

        var wav = new byte[44 + dataSize];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataSize);
        "WAVE"u8.CopyTo(span[8..]);

        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], clip.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], clip.SampleRate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], bitsPerSample);

        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataSize);
        clip.Pcm16.CopyTo(span[44..]);
        return wav;
    }
}
