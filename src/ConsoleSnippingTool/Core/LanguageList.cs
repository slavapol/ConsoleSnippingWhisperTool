using System.Collections;

namespace ConsoleSnippingTool.Core;

/// The language codes a Dictation is expected in, as config.json lists them under dictation.languages ("en", "ru").
/// A value: two lists with the same codes in the same order are equal, so a Config holding one still compares as a
/// record; it prints as the codes joined by ", ", the text the fallback report shows (en, ru). The codes are kept as
/// written and never checked here: the Service accepts or rejects them (spec: Configuration).
public sealed class LanguageList : IReadOnlyList<string>, IEquatable<LanguageList>
{
    /// English and Russian, the spec's default.
    public static readonly LanguageList Default = new("en", "ru");

    /// No hint: the Service detects the language on its own.
    public static readonly LanguageList None = new();

    private readonly string[] _codes;

    public LanguageList(params IEnumerable<string> codes) => _codes = codes.ToArray();

    public int Count => _codes.Length;

    public string this[int index] => _codes[index];

    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_codes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(LanguageList? other) => other is not null && _codes.AsSpan().SequenceEqual(other._codes);

    public override bool Equals(object? obj) => Equals(obj as LanguageList);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var code in _codes)
        {
            hash.Add(code, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(", ", _codes);
}
