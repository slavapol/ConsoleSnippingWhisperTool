namespace ConsoleSnippingTool.Core;

/// What the Cleanup pass needs. Instructions is the core's constant prompt.
public sealed record CleanupRequest(string ApiKey, string Model, string Instructions);
