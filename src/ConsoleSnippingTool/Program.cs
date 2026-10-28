using ConsoleSnippingTool.Core;
using ConsoleSnippingTool.Win32;

namespace ConsoleSnippingTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // These must precede the first window, and the platform's constructor creates the message window and the
        // tray icon. ApplicationConfiguration.Initialize() is not used: its generated body also calls
        // SetHighDpiMode(SystemAware), while DPI awareness is Per-Monitor-V2 from the manifest (the WFO0003
        // suppression in the csproj records that choice), so only its other two calls are made here.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var platform = new Win32Platform();
        var core = new SnipperCore(platform);
        platform.Attach(core);

        // Exceptions escaping the message loop (hotkey, timer, tray, posted mouse events) leave the tool running after
        // the report; those from other threads end the process after it, but at least say so.
        Application.ThreadException += (_, e) => Report(core, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                Report(core, exception);
            }
        };

        try
        {
            core.Start();
        }
        catch (Exception e)
        {
            Report(core, e); // the balloon shows the tray icon, so Exit is still reachable
        }

        Application.Run();
    }

    private static void Report(SnipperCore core, Exception exception)
    {
        try
        {
            core.OnUnhandledException(exception);
        }
        catch (Exception)
        {
            // Nothing is left to report through; a failure here must not take the message loop down.
        }
    }
}
