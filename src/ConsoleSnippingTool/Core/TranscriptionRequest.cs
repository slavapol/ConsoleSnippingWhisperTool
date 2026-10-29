namespace ConsoleSnippingTool.Core;

/// What the upload transport needs for one Transcript. ApiKey is never logged.
public sealed record TranscriptionRequest(string ApiKey, string Model, string? Language, string? Prompt)
{
    /// The record's usual shape minus the key, so nothing that formats a request can leak it (spec: user story 39).
    public override string ToString() =>
        $"{nameof(TranscriptionRequest)} {{ Model = {Model}, Language = {Language ?? "null"}, Prompt = {Prompt ?? "null"} }}";
}
