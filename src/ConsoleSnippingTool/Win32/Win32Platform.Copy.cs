namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// Copy (spec: Delivery): the Transcript as ordinary Unicode text through the shared half-second retry, with none
    /// of the Insert's history and cloud markers, snapshot or restore. It replaces whatever was on the clipboard, a
    /// Capture's image included, as any Ctrl+C would, and shows up in Win+V like one. False only when the clipboard
    /// stayed locked. Runs on the message-loop thread, the only STA thread, which every Clipboard call needs.
    public bool TryCopyText(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, autoConvert: false, text);
        return TrySetClipboard(data);
    }
}
