namespace ConsoleSnippingTool.Tests;

/// A complete config.json in the shape the spec's Configuration section shows (and Config.ToJson writes), so a test
/// states only the field it is about. With no arguments it is the default file. The two Hotkeys are plain text (the
/// template quotes them); every other value is a JSON literal, quotes included (null, "none", "x.wav", 120, "yes",
/// false, ["en", "ru"]), so a test can state a wrong type as easily as a right value.
internal static class ConfigJson
{
    public static string With(
        string hotkey = "Ctrl+Win+Z",
        string armedTimeoutSeconds = "30",
        string ready = "null",
        string corner = "null",
        string sent = "null",
        string done = "null",
        string apiKey = "null",
        string dictationHotkey = "Ctrl+Win+X",
        string listeningTimeoutSeconds = "120",
        string insert = "false",
        string model = "\"gpt-transcribe\"",
        string languages = "[\"en\", \"ru\"]",
        string prompt = "null",
        string cleanup = "false",
        string cleanupModel = "\"gpt-6-luna\"") => $$"""
        {
          "hotkey": "{{hotkey}}",
          "armedTimeoutSeconds": {{armedTimeoutSeconds}},
          "sounds": {
            "ready": {{ready}},
            "corner": {{corner}},
            "sent": {{sent}},
            "done": {{done}}
          },
          "openAi": {
            "apiKey": {{apiKey}}
          },
          "dictation": {
            "hotkey": "{{dictationHotkey}}",
            "listeningTimeoutSeconds": {{listeningTimeoutSeconds}},
            "insert": {{insert}},
            "model": {{model}},
            "languages": {{languages}},
            "prompt": {{prompt}},
            "cleanup": {{cleanup}},
            "cleanupModel": {{cleanupModel}}
          }
        }
        """;
}
