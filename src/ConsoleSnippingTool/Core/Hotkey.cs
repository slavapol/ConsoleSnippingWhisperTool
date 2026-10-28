namespace ConsoleSnippingTool.Core;

/// VirtualKey is the Win32 VK code. Text is the canonical display form, e.g. "Ctrl+Win+Z".
public readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey, string Text)
{
    public static Hotkey Default => new(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x5A, "Ctrl+Win+Z");
}
