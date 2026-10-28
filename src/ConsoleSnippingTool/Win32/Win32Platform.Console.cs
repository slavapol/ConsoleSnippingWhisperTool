using System.Text;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    // Kept in a field so the delegate handed to SetConsoleCtrlHandler is never collected. It claims every event
    // (Ctrl+C, Ctrl+Break, close), so none of them terminates the process.
    private readonly NativeMethods.ConsoleCtrlHandler _consoleCtrlHandler;

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
}
