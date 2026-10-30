using System.Globalization;

namespace ConsoleSnippingTool.Core;

/// The state machine behind the tool. It receives events from the Win32 adapter through its public On* methods and
/// answers only by calling <see cref="IPlatform"/>. Split over several files so tickets edit disjoint parts: this
/// one holds what both jobs share (the state, the log, and the Esc and timer events, which dispatch by state), then
/// Startup (Start, replace, exit, unhandled errors), Capture (Hotkey, Corners, cancel), Dictation (Hotkey, Release,
/// Transcript, Cleanup) and Console (Show, Hide).
public sealed partial class SnipperCore
{
    public const string ToolName = "ConsoleSnippingTool";

    /// How many log lines Show can replay (spec: Console and log).
    private const int LogCapacity = 200;

    /// Idle, then one job at a time: Armed and FirstCorner belong to Capture, Listening and Transcribing to Dictation.
    private enum State { Idle, Armed, FirstCorner, Listening, Transcribing }

    private readonly IPlatform _platform;
    private readonly Func<DateTime> _clock;
    private readonly LogRing _log = new(LogCapacity);
    private State _state = State.Idle;
    private Config _config = new();

    /// The API key resolved at startup from openAi.apiKey or else OPENAI_API_KEY; null when there is none. Held in
    /// memory only: never logged, never written to config.json (spec: user stories 22 and 39).
    private string? _apiKey;

    /// Reported at startup, and again on each Dictation Hotkey press while there is no key (spec: user story 23).
    private const string NoApiKeyMessage =
        "Dictation needs an API key: set openAi.apiKey in config.json or the OPENAI_API_KEY environment variable";

    /// When the Dictation Hotkey press that led to Listening arrived, by the injected clock, taken before the microphone
    /// is opened; the Tap threshold is measured from here (spec: Hotkey, release and Tap).
    private DateTime _listeningSince;

    /// Numbers each request the Service is sent, so a result from an earlier Dictation is recognised and ignored.
    /// 0 until the first Release; incremented when a request is sent.
    private int _dictationSequence;

    /// When the Release sent the audio; the wait the Inserted log line reports is measured from here.
    private DateTime _sentAt;

    /// The Transcript as the Service returned it, kept while a Cleanup is in flight so a failed Cleanup can still
    /// Insert what was said (spec: user story 20).
    private string? _rawTranscript;

    /// True between Show and Hide.
    private bool ConsoleShown { get; set; }

    /// <param name="clock">Local time for log timestamps; defaults to <see cref="DateTime.Now"/>. Tests pin it.</param>
    public SnipperCore(IPlatform platform, Func<DateTime>? clock = null)
    {
        _platform = platform;
        _clock = clock ?? (() => DateTime.Now);
    }

    /// Timestamps a line, keeps it in the ring and writes it to the Console when it is shown.
    private void Log(string line)
    {
        var stamped = $"{_clock().ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {line}";
        _log.Append(stamped);
        if (ConsoleShown)
        {
            _platform.WriteConsoleLine(stamped);
        }
    }

    /// A problem the user should hear about now: a balloon from the tray, then the same text as a log line.
    private void Report(string message)
    {
        _platform.ShowBalloon(ToolName, message);
        Log(message);
    }

    // The two events both jobs receive, each dispatched by state to the job's own private method.

    /// Esc: the cancel key registered while Armed, or the key the keyboard hook swallows while Listening; any other call
    /// is a stray one.
    public void OnCancelKey()
    {
        switch (_state)
        {
            case State.Armed:
            case State.FirstCorner:
                Cancel(CancelReason.Escape);
                break;
            case State.Listening:
                Discard(DiscardReason.Escape);
                break;
        }
    }

    /// The one timer: the Armed timeout or the Listening cap, stopped on leaving either, so an Idle call is a late one.
    public void OnTimerElapsed()
    {
        switch (_state)
        {
            case State.Armed:
            case State.FirstCorner:
                Cancel(CancelReason.Timeout);
                break;
            case State.Listening:
                Release(byCap: true);
                break;
        }
    }
}
