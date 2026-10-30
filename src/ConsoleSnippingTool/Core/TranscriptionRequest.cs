namespace ConsoleSnippingTool.Core;

/// What the upload transport needs for one Transcript; Languages are the expected ones from config.json, possibly
/// none. ApiKey is never logged.
public sealed record TranscriptionRequest(string ApiKey, string Model, IReadOnlyList<string> Languages, string? Prompt)
{
    /// The record's usual shape minus the key, so nothing that formats a request can leak it (spec: user story 39).
    public override string ToString() =>
        $"{nameof(TranscriptionRequest)} {{ Model = {Model}, Languages = {LanguagesText}, Prompt = {Prompt ?? "null"} }}";

    private string LanguagesText => Languages.Count == 0 ? "none" : string.Join(", ", Languages);
}
