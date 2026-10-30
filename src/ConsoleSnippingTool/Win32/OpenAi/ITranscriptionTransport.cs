using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32.OpenAi;

/// How the audio of one Dictation reaches the Service and comes back as a Transcript (ADR-0004). UploadTransport is
/// the one built; a streaming transport would implement this too and be chosen by config, with the core none the wiser.
internal interface ITranscriptionTransport
{
    /// Returns the Transcript with the languages the Service detected (empty when it reported none). Throws when it
    /// cannot, with the failure reason as the message: ServiceException for an error status, HttpRequestException when
    /// nothing answered, OperationCanceledException when the token fired, FormatException for a response without text.
    Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken cancellationToken);
}
