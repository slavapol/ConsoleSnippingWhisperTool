using System.Globalization;

namespace ConsoleSnippingTool.Core;

/// What the Cleanup pass needs. Instructions is the core's constant prompt; Timeout is how long the adapter lets the
/// request run, the same as the Transcription request of the same Dictation.
public sealed record CleanupRequest(string ApiKey, string Model, string Instructions, TimeSpan Timeout)
{
    /// The record's usual shape minus the key, so nothing that formats a request can leak it (spec: user story 39).
    public override string ToString() =>
        $"{nameof(CleanupRequest)} {{ Model = {Model}, Instructions = {Instructions}, Timeout = {TimeoutText} }}";

    private string TimeoutText => $"{Timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s";
}
