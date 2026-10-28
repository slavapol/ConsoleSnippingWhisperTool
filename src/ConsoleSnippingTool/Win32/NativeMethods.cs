using System.Runtime.InteropServices;

namespace ConsoleSnippingTool.Win32;

/// Every P/Invoke the adapter uses, in one place. Names and signatures mirror the Win32 declarations.
internal static class NativeMethods
{
    // Window messages
    public const int WM_HOTKEY = 0x0312;
    public const int WM_APP = 0x8000;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;

    // RegisterHotKey
    public const uint MOD_NOREPEAT = 0x4000;
    public const uint VK_ESCAPE = 0x1B;

    // SetWindowsHookEx
    public const int WH_MOUSE_LL = 14;
    public const int HC_ACTION = 0;
    public const uint LLMHF_INJECTED = 0x00000001;

    // System menu
    public const uint SC_CLOSE = 0xF060;
    public const uint MF_BYCOMMAND = 0x00000000;

    // CreateWindowEx parent for a message-only window
    public const nint HWND_MESSAGE = -3;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    /// WH_MOUSE_LL callback. lParam is passed by reference so the struct is read in place, with no allocation.
    public delegate nint LowLevelMouseProc(int nCode, nint wParam, ref MSLLHOOKSTRUCT lParam);

    [return: MarshalAs(UnmanagedType.Bool)]
    public delegate bool ConsoleCtrlHandler(uint ctrlType);

    // Hotkeys

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    // Low-level mouse hook

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookExW(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, ref MSLLHOOKSTRUCT lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint GetModuleHandleW(string? lpModuleName);

    // Messages

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    // Console

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    public static extern nint GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler? handlerRoutine, [MarshalAs(UnmanagedType.Bool)] bool add);

    [DllImport("user32.dll")]
    public static extern nint GetSystemMenu(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteMenu(nint hMenu, uint uPosition, uint uFlags);

    // Tray icon

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(nint hIcon);
}
