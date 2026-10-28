namespace ConsoleSnippingTool.Core;

/// What a signal plays. BuiltIn = the synthesised click; None = silent; File = a .wav path.
public abstract record SoundSource
{
    public sealed record BuiltIn : SoundSource;
    public sealed record None : SoundSource;
    public sealed record File(string Path) : SoundSource;
}
