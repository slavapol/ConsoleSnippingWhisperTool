namespace ConsoleSnippingTool.Core;

/// What a signal plays. BuiltIn = the synthesised click; None = silent; File = a .wav path.
public abstract record SoundSource
{
    public sealed record BuiltIn : SoundSource;
    public sealed record None : SoundSource;
    public sealed record File(string Path) : SoundSource;

    /// BuiltIn, None or File(path): the one text form, for log lines and for the tests' recorded calls.
    public sealed override string ToString() => this is File file ? $"File({file.Path})" : GetType().Name;
}
