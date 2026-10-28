namespace ConsoleSnippingTool.Core;

/// The state machine behind the tool. It receives events from the Win32 adapter through its public On* methods and
/// answers only by calling <see cref="IPlatform"/>. Split over several files so tickets edit disjoint parts:
/// Startup (Start, replace, exit), Capture (Hotkey, Corners, cancel) and Console (Show, Hide).
public sealed partial class SnipperCore
{
    public const string ToolName = "ConsoleSnippingTool";

    private enum State { Idle, Armed, FirstCorner }

    private readonly IPlatform _platform;
    private State _state = State.Idle;
    private Config _config = new();

    /// True between Show and Hide. Ticket 05 sets it from the Console file.
    private bool ConsoleShown { get; set; }

    public SnipperCore(IPlatform platform)
    {
        _platform = platform;
    }

    /// Writes a line to the Console when it is shown. Ticket 05 adds the timestamp and the log ring.
    private void Log(string line)
    {
        if (ConsoleShown)
        {
            _platform.WriteConsoleLine(line);
        }
    }
}
