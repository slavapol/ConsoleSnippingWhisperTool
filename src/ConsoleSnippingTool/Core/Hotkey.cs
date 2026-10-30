namespace ConsoleSnippingTool.Core;

/// VirtualKey is the Win32 VK code. Text is the canonical display form, e.g. "Ctrl+Win+Z".
public readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey, string Text)
{
    public static Hotkey Default => new(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x5A, "Ctrl+Win+Z");

    public static Hotkey DictationDefault => new(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x58, "Ctrl+Win+X");

    /// The Playback Hotkey's default. Windows 11 takes Win+A for Quick Settings but documents nothing for Ctrl+Win+A.
    public static Hotkey PlaybackDefault => new(HotkeyModifiers.Control | HotkeyModifiers.Win, 0x41, "Ctrl+Win+A");

    private const int VkDigit0 = 0x30;
    private const int VkLetterA = 0x41;
    private const int VkF1 = 0x70;
    private const int FunctionKeyCount = 24;

    /// Parses the config.json grammar: modifiers (Ctrl/Control, Alt, Shift, Win/Windows) and exactly one key (a letter,
    /// a digit or F1–F24) joined by "+", case-insensitive, whitespace around tokens ignored. At least one modifier and
    /// exactly one key; anything else (an unknown or repeated token, an empty token) is invalid.
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (text is null)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        int? virtualKey = null;
        foreach (var token in text.Split('+').Select(t => t.Trim()))
        {
            if (TryParseModifier(token, out var modifier))
            {
                if ((modifiers & modifier) != 0)
                {
                    return false;
                }

                modifiers |= modifier;
            }
            else if (TryParseKey(token, out var key))
            {
                if (virtualKey is not null)
                {
                    return false;
                }

                virtualKey = key;
            }
            else
            {
                return false;
            }
        }

        if (modifiers == HotkeyModifiers.None || virtualKey is null)
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, virtualKey.Value, CanonicalText(modifiers, virtualKey.Value));
        return true;
    }

    private static bool TryParseModifier(string token, out HotkeyModifiers modifier)
    {
        modifier = token.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => HotkeyModifiers.Control,
            "ALT" => HotkeyModifiers.Alt,
            "SHIFT" => HotkeyModifiers.Shift,
            "WIN" or "WINDOWS" => HotkeyModifiers.Win,
            _ => HotkeyModifiers.None,
        };
        return modifier != HotkeyModifiers.None;
    }

    private static bool TryParseKey(string token, out int virtualKey)
    {
        virtualKey = 0;
        if (token.Length == 1 && char.IsAsciiLetter(token[0]))
        {
            virtualKey = VkLetterA + (char.ToUpperInvariant(token[0]) - 'A');
            return true;
        }

        if (token.Length == 1 && char.IsAsciiDigit(token[0]))
        {
            virtualKey = VkDigit0 + (token[0] - '0');
            return true;
        }

        if (token.Length is >= 2 and <= 3
            && char.ToUpperInvariant(token[0]) == 'F'
            && int.TryParse(token.AsSpan(1), System.Globalization.NumberStyles.None, null, out var number)
            && number is >= 1 and <= FunctionKeyCount)
        {
            virtualKey = VkF1 + number - 1;
            return true;
        }

        return false;
    }

    /// Modifiers in the order Ctrl, Alt, Shift, Win, then the key, joined by "+".
    private static string CanonicalText(HotkeyModifiers modifiers, int virtualKey)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyText(virtualKey));
        return string.Join('+', parts);
    }

    private static string KeyText(int virtualKey) => virtualKey switch
    {
        >= VkDigit0 and < VkDigit0 + 10 => ((char)('0' + virtualKey - VkDigit0)).ToString(),
        >= VkLetterA and < VkLetterA + 26 => ((char)('A' + virtualKey - VkLetterA)).ToString(),
        >= VkF1 and < VkF1 + FunctionKeyCount => "F" + (virtualKey - VkF1 + 1),
        _ => throw new ArgumentOutOfRangeException(nameof(virtualKey), virtualKey, null),
    };
}
