using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Tests;

/// The log ring as Show replays it, for the job tests that assert on Console lines. The header Show prints first
/// ends with its one blank line, and no log line is ever blank (each starts with a timestamp), so the ring is found
/// without knowing how many lines the header has: the header's own text is ConsoleTests' business.
internal static class ConsoleReplay
{
    private const string LinePrefix = "WriteConsoleLine(";

    /// Every line after the header, timestamps included. Call it last, because Show itself adds calls.
    public static List<string> Logged(FakePlatform fake, SnipperCore core)
    {
        var before = fake.Calls.Count;
        core.OnShow();
        return fake.Calls
            .Skip(before + 1) // ShowConsole()
            .SkipWhile(call => call != LinePrefix + ")")
            .Skip(1) // the header's blank line
            .TakeWhile(call => call.StartsWith(LinePrefix, StringComparison.Ordinal))
            .Select(call => call[LinePrefix.Length..^1])
            .ToList();
    }
}
