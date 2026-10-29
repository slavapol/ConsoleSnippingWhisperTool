namespace ConsoleSnippingTool.Core;

/// What the Cleanup pass needs. Instructions is the core's constant prompt.
public sealed record CleanupRequest(string ApiKey, string Model, string Instructions)
{
    /// The record's usual shape minus the key, so nothing that formats a request can leak it (spec: user story 39).
    public override string ToString() =>
        $"{nameof(CleanupRequest)} {{ Model = {Model}, Instructions = {Instructions} }}";
}
