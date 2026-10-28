using System.Text;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    /// How long a console close gives the message loop to run Exit before the console host ends the process anyway.
    private static readonly TimeSpan ConsoleCloseExitWait = TimeSpan.FromSeconds(3);

    // Kept in a field so the delegate handed to SetConsoleCtrlHandler is never collected.
    private readonly NativeMethods.ConsoleCtrlHandler _consoleCtrlHandler;

    // Set by Exit(), the last step of the core's OnExit, so a waiting console close handler knows the Hotkey is
    // released and the tray icon removed.
    private readonly ManualResetEventSlim _exited = new(initialState: false);

    public void ShowConsole()
    {
        NativeMethods.AllocConsole();
        var console = NativeMethods.GetConsoleWindow();
        var systemMenu = NativeMethods.GetSystemMenu(console, bRevert: false);
        NativeMethods.DeleteMenu(systemMenu, NativeMethods.SC_CLOSE, NativeMethods.MF_BYCOMMAND);
        NativeMethods.SetConsoleCtrlHandler(_consoleCtrlHandler, add: true);

        // The process started without a console, so Console.Out points nowhere; re-open it on the new one.
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true });
    }

    public void HideConsole()
    {
        Console.Out.Flush();
        Console.SetOut(TextWriter.Null);
        NativeMethods.SetConsoleCtrlHandler(_consoleCtrlHandler, add: false);
        NativeMethods.FreeConsole();
    }

    /// A no-op while no console is allocated: Console.Out is then TextWriter.Null (after Hide) or a writer over
    /// Stream.Null (a WinExe starts with no standard output).
    public void WriteConsoleLine(string line) => Console.Out.WriteLine(line);

    /// Runs on a thread the console host creates. Ctrl+C and Ctrl+Break are claimed and ignored (ADR-0001). A close
    /// cannot be ignored: under classic conhost the X is already gone from the system menu, but under Windows
    /// Terminal GetConsoleWindow() is not the visible window, the SC_CLOSE removal has no effect, and the process is
    /// terminated once this returns whatever it returns. So a close becomes a clean Exit: posted to the message loop,
    /// which releases the Hotkey and removes the tray icon, and awaited for a bounded time before returning.
    private bool OnConsoleCtrl(uint ctrlType)
    {
        if (ctrlType == NativeMethods.CTRL_CLOSE_EVENT)
        {
            NativeMethods.PostMessageW(_window.Handle, MessageWindow.WM_EXIT_REQUESTED, 0, 0);
            _exited.Wait(ConsoleCloseExitWait);
        }

        return true;
    }
}
