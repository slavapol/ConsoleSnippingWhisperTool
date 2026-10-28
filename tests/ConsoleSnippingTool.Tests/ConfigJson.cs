namespace ConsoleSnippingTool.Tests;

/// A complete config.json in the shape the spec's Configuration section shows (and Config.ToJson writes), so a test
/// states only the field it is about. With no arguments it is the default file. Sound values are JSON literals:
/// null, "none" or "x.wav", quotes included.
internal static class ConfigJson
{
    public static string With(
        string hotkey = "Ctrl+Win+Z",
        int armedTimeoutSeconds = 30,
        string ready = "null",
        string corner = "null",
        string done = "null") => $$"""
        {
          "hotkey": "{{hotkey}}",
          "armedTimeoutSeconds": {{armedTimeoutSeconds}},
          "sounds": {
            "ready": {{ready}},
            "corner": {{corner}},
            "done": {{done}}
          }
        }
        """;
}
