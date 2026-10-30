using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32.OpenAi;

/// The upload transport: after the Release, the whole clip as one WAV in one multipart POST to
/// v1/audio/transcriptions (ADR-0004). The WAV container goes on here, on the caller's thread pool thread.
internal sealed class UploadTransport : ITranscriptionTransport
{
    public async Task<(string Text, IReadOnlyList<string> Languages)> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken cancellationToken)
    {
        var wav = Wav.FromPcm16(clip.Pcm16, clip.SampleRate);
        var form = OpenAiJson.TranscriptionForm(wav, request.Model, request.Languages, request.Prompt);
        var body = await OpenAiHttp
            .SendAsync(HttpMethod.Post, "v1/audio/transcriptions", request.ApiKey, form, cancellationToken)
            .ConfigureAwait(false);
        return OpenAiJson.ParseTranscript(body);
    }
}
