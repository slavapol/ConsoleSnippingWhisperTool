using System.Collections;

namespace ConsoleSnippingTool.Core;

/// The in-memory log the Console replays on Show (ADR-0001: the console buffer is lost on Hide).
/// Keeps the last <c>capacity</c> lines, dropping the oldest; enumerates oldest first.
public sealed class LogRing : IEnumerable<string>
{
    private readonly int _capacity;
    private readonly Queue<string> _lines;

    public LogRing(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _lines = new Queue<string>(capacity);
    }

    public void Append(string line)
    {
        if (_lines.Count == _capacity)
        {
            _lines.Dequeue();
        }

        _lines.Enqueue(line);
    }

    public IEnumerator<string> GetEnumerator() => _lines.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
