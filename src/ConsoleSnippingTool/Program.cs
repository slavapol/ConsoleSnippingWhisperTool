namespace ConsoleSnippingTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Placeholder message loop: runs an empty ApplicationContext and exits as soon as the loop goes idle.
        // Ticket 08 replaces this with the tray icon and hotkey loop.
        using var context = new ApplicationContext();
        Application.Idle += (_, _) => context.ExitThread();
        Application.Run(context);
    }
}
