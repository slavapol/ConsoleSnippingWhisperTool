namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    // The paste with restore (ADR-0003) is ticket 07. Until then nothing is pasted and the core reports why.

    public bool TryInsertText(string text, out string reason)
    {
        reason = "insert not implemented";
        return false;
    }
}
