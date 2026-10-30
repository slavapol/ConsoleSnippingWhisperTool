namespace ConsoleSnippingTool.Win32.OpenAi;

/// What the Service returned for one Dictation: the Transcript's text and the languages the Service detected in it
/// (empty when it reported none). The transport's result, parsed by OpenAiJson; the adapter hands its two parts to
/// the core through OnTranscript, so the seam itself carries no adapter type.
public sealed record Transcript(string Text, IReadOnlyList<string> Languages);
