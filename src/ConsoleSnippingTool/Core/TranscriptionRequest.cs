using System.Globalization;

namespace ConsoleSnippingTool.Core;

/// What the upload transport needs for one Transcript; Languages are the expected ones from config.json, possibly
/// none. Timeout is how long the adapter lets the request run, computed by the core. ApiKey is never logged.
public sealed record TranscriptionRequest(
    string ApiKey, string Model, IReadOnlyList<string> Languages, string? Prompt, TimeSpan Timeout)
{
    /// The record's usual shape minus the key, so nothing that formats a request can leak it (spec: user story 39).
    public override string ToString() =>
        $"{nameof(TranscriptionRequest)} {{ Model = {Model}, Languages = {LanguagesText}, Prompt = {Prompt ?? "null"}, Timeout = {TimeoutText} }}";

    private string LanguagesText => Languages.Count == 0 ? "none" : string.Join(", ", Languages);

    private string TimeoutText => $"{Timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s";
}
