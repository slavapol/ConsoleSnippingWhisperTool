using System.Runtime.InteropServices;

namespace ConsoleSnippingTool.Win32;

/// What the Insert (ADR-0003) needs beyond WinForms' Clipboard: the synthesised Ctrl+V, the physical modifier poll and
/// the clipboard sequence number. Layouts and values checked against winuser.h on Microsoft Learn, 2026-10-29.
internal static partial class NativeMethods
{
    // Virtual keys for SendInput (WORD wVk). winuser.h has no define for the letter keys: 'V' is its ASCII code.
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_V = 0x56;

    // Virtual keys for GetAsyncKeyState (int vKey): the left and right instances of the four modifiers.
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;

    // INPUT.type
    public const uint INPUT_KEYBOARD = 1;

    // KEYBDINPUT.dwFlags
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    /// The anonymous union inside INPUT. Only ki is ever written, but mi is the largest member and fixes the size:
    /// SendInput rejects the whole call unless cbSize is sizeof(INPUT), 40 bytes on x64 (28 on x86).
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    // Synthesised input

    /// Returns the number of events inserted. Fewer than cInputs means blocked: by another thread (zero), or by UIPI
    /// when the foreground window is of higher integrity, which sets no error code at all.
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    /// The most significant bit, which is the sign bit, is set while the key is physically down: negative means down.
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    // Clipboard

    /// Incremented whenever the clipboard's contents change or it is emptied.
    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();
}
