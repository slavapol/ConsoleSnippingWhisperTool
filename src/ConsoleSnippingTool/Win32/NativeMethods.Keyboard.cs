using System.Runtime.InteropServices;

namespace ConsoleSnippingTool.Win32;

/// What the keyboard hook adds to the shared P/Invoke: the WH_KEYBOARD_LL constants, the key codes the hook compares
/// a key-up against, and the two hook entry points overloaded for the keyboard delegate and struct.
internal static partial class NativeMethods
{
    // Window messages
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYUP = 0x0105;

    // SetWindowsHookEx
    public const int WH_KEYBOARD_LL = 13;

    // Virtual keys of the modifiers a chord can hold. A low-level hook reports the physical left and right codes; the
    // generic VK_CONTROL, VK_MENU (Alt) and VK_SHIFT are what injected input may carry. Win has no generic code.
    public const uint VK_SHIFT = 0x10;
    public const uint VK_CONTROL = 0x11;
    public const uint VK_MENU = 0x12;
    public const uint VK_LWIN = 0x5B;
    public const uint VK_RWIN = 0x5C;
    public const uint VK_LSHIFT = 0xA0;
    public const uint VK_RSHIFT = 0xA1;
    public const uint VK_LCONTROL = 0xA2;
    public const uint VK_RCONTROL = 0xA3;
    public const uint VK_LMENU = 0xA4;
    public const uint VK_RMENU = 0xA5;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    /// WH_KEYBOARD_LL callback. lParam is passed by reference so the struct is read in place, with no allocation.
    public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, ref KBDLLHOOKSTRUCT lParam);

    // Low-level keyboard hook: the same entry points as the mouse hook, overloaded on the delegate and struct type.
    // UnhookWindowsHookEx and GetModuleHandleW are shared with it.

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, ref KBDLLHOOKSTRUCT lParam);
}
