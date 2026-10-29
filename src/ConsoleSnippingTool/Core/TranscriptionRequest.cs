namespace ConsoleSnippingTool.Core;

/// What the upload transport needs for one Transcript. ApiKey is never logged.
public sealed record TranscriptionRequest(string ApiKey, string Model, string? Language, string? Prompt);
