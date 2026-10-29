namespace ConsoleSnippingTool.Tests;

/// A test API key, so a test that is not about the key states in one word that Start finds one and reports nothing
/// about Dictation. Only the startup tests about the key itself spell out where it comes from.
internal static class ApiKey
{
    public const string Value = "sk-test";

    /// For FakePlatform.Environment: the key in OPENAI_API_KEY, the place the default config.json points at.
    public static Dictionary<string, string?> InEnvironment => new() { ["OPENAI_API_KEY"] = Value };
}
