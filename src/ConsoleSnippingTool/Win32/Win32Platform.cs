using System.ComponentModel;
using System.Diagnostics;
using ConsoleSnippingTool.Core;

namespace ConsoleSnippingTool.Win32;

/// The IPlatform for real Windows. Every member is a direct translation of its contract into Win32 or WinForms calls;
/// the decisions live in SnipperCore. Construct it on the STA thread that will run the message loop, then Attach the
/// core before core.Start() (the core takes the platform in its constructor, so this order is forced):
///
///     var platform = new Win32Platform();
///     var core = new SnipperCore(platform);
///     platform.Attach(core);
///     core.Start();
///     Application.Run();
///
/// Split over several files by concern: this one (instance, config file, exit), Capture (hotkeys, mouse hook, Armed
/// timer, screen to clipboard), Sounds, Console and Tray.
public sealed partial class Win32Platform : IPlatform
{
    private const string InstanceMutexName = @"Local\ConsoleSnippingTool.Instance";
    private const string ReplaceEventName = @"Local\ConsoleSnippingTool.Replace";
    private const string ConfigFileName = "config.json";

    /// How long TerminatePreviousInstance waits for a killed process to be gone, so its mutex is free on return.
    private const int KillWaitMilliseconds = 2000;

    private static readonly string ExeFolder = AppContext.BaseDirectory;

    private readonly MessageWindow _window;
    private SnipperCore? _core;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _replaceEvent;
    private RegisteredWaitHandle? _replaceWatch;

    public Win32Platform()
    {
        _window = new MessageWindow(
            onHotkey: () => AttachedCore.OnHotkey(),
            onCancelKey: () => AttachedCore.OnCancelKey(),
            onReplaceRequested: () => AttachedCore.OnReplaceRequested());
        _mouseProc = MouseHookCallback;
        _armedTimer = new System.Windows.Forms.Timer();
        _armedTimer.Tick += OnArmedTimerTick;
        _consoleCtrlHandler = _ => true;
        _showItem = new ToolStripMenuItem("Show", null, (_, _) => AttachedCore.OnShow());
        _hideItem = new ToolStripMenuItem("Hide", null, (_, _) => AttachedCore.OnHide());
        _trayMenu = new ContextMenuStrip
        {
            Items = { _showItem, _hideItem, new ToolStripMenuItem("Exit", null, (_, _) => AttachedCore.OnExit()) },
        };
        _trayIconHandle = CreateTrayIconHandle();
        _trayIcon = new NotifyIcon
        {
            Icon = Icon.FromHandle(_trayIconHandle),
            ContextMenuStrip = _trayMenu,
            Text = SnipperCore.ToolName,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => AttachedCore.OnShow();
    }

    /// The core this adapter reports events to. Must be called before core.Start().
    public void Attach(SnipperCore core) => _core = core;

    private SnipperCore AttachedCore =>
        _core ?? throw new InvalidOperationException("Attach(core) must be called before Start().");

    // Single instance

    public bool TryAcquireInstance()
    {
        _instanceMutex ??= new Mutex(initiallyOwned: false, InstanceMutexName);
        if (!WaitForInstanceMutex(TimeSpan.Zero))
        {
            return false;
        }

        StartReplaceWatch();
        return true;
    }

    public void SignalPreviousInstance()
    {
        using var replace = new EventWaitHandle(initialState: false, EventResetMode.ManualReset, ReplaceEventName);
        replace.Set();
    }

    public bool WaitForPreviousInstanceExit(TimeSpan timeout)
    {
        _instanceMutex ??= new Mutex(initiallyOwned: false, InstanceMutexName);
        return WaitForInstanceMutex(timeout);
    }

    public void TerminatePreviousInstance()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            return;
        }

        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath)))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                try
                {
                    if (string.Equals(process.MainModule?.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill();
                        process.WaitForExit(KillWaitMilliseconds);
                    }
                }
                catch (Win32Exception)
                {
                    // Not ours to inspect (access denied); nothing to terminate.
                }
                catch (InvalidOperationException)
                {
                    // Exited between enumeration and inspection.
                }
            }
        }
    }

    /// Acquires the mutex; one abandoned by a dead previous Instance counts as acquired.
    private bool WaitForInstanceMutex(TimeSpan timeout)
    {
        try
        {
            return _instanceMutex!.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    /// Resets the replace event (a stale signal must not fire at us) and waits for it on the pool, marshalling the
    /// request onto the message loop as a posted message.
    private void StartReplaceWatch()
    {
        if (_replaceEvent is not null)
        {
            return;
        }

        _replaceEvent = new EventWaitHandle(initialState: false, EventResetMode.ManualReset, ReplaceEventName);
        _replaceEvent.Reset();
        var window = _window.Handle;
        _replaceWatch = ThreadPool.RegisterWaitForSingleObject(
            _replaceEvent,
            (_, _) => NativeMethods.PostMessageW(window, MessageWindow.WM_REPLACE_REQUESTED, 0, 0),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: true);
    }

    // Config file beside the exe

    public string? ReadConfigText()
    {
        var path = Path.Combine(ExeFolder, ConfigFileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void WriteConfigText(string json) => File.WriteAllText(Path.Combine(ExeFolder, ConfigFileName), json);

    // Process

    public void Exit() => Application.ExitThread();
}
