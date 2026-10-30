using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
/// Split over several files by concern: this one (instance, config file, environment, what both jobs share: the
/// one-shot timer and the clipboard write; exit), Capture (Capture Hotkey, mouse hook, screen to clipboard), Keyboard
/// (Dictation Hotkey, keyboard hook), Microphone, Service (transport to the Service), Copy (the Transcript as a plain
/// clipboard write), Insert (paste with restore), Sounds, Console and Tray.
public sealed partial class Win32Platform : IPlatform
{
    private const string InstanceMutexName = @"Local\ConsoleSnippingTool.Instance";
    private const string ReplaceEventName = @"Local\ConsoleSnippingTool.Replace";

    /// How long TerminatePreviousInstance waits for a killed process to be gone, so its mutex is free on return.
    private const int KillWaitMilliseconds = 2000;

    private static readonly string ExeFolder = AppContext.BaseDirectory;

    private readonly MessageWindow _window;
    private readonly System.Windows.Forms.Timer _timer;
    private SnipperCore? _core;
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;
    private EventWaitHandle? _replaceEvent;
    private RegisteredWaitHandle? _replaceWatch;

    public Win32Platform()
    {
        _window = new MessageWindow(() => AttachedCore);
        _mouseProc = MouseHookCallback;
        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += OnTimerTick;
        _consoleCtrlHandler = OnConsoleCtrl;
        _showItem = new ToolStripMenuItem("Show", null, (_, _) => AttachedCore.OnShow());
        _hideItem = new ToolStripMenuItem("Hide", null, (_, _) => AttachedCore.OnHide());
        _trayMenu = new ContextMenuStrip
        {
            Items = { _showItem, _hideItem, new ToolStripMenuItem("Exit", null, (_, _) => AttachedCore.OnExit()) },
        };
        _trayIconHandle = CreateTrayIconHandle();
        // Not yet Visible: a new Instance may wait up to 5 s for the old one, and two icons would show meanwhile. The
        // first SetTrayTooltip or ShowBalloon, both after the handshake, shows it.
        _trayIcon = new NotifyIcon
        {
            Icon = Icon.FromHandle(_trayIconHandle),
            ContextMenuStrip = _trayMenu,
            Text = SnipperCore.ToolName,
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
        // Already owned after WaitForPreviousInstanceExit: a second WaitOne would re-enter the mutex and need a
        // second release.
        if (!_ownsInstanceMutex && !WaitForInstanceMutex(TimeSpan.Zero))
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
            _ownsInstanceMutex = _instanceMutex!.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            _ownsInstanceMutex = true;
        }

        return _ownsInstanceMutex;
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
        var path = Path.Combine(ExeFolder, Config.FileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void WriteConfigText(string json) => File.WriteAllText(Path.Combine(ExeFolder, Config.FileName), json);

    // Environment

    public string? ReadEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    // Timer (one shot, on the message loop; the Armed timeout and the Listening cap share it)

    public void StartTimer(TimeSpan duration)
    {
        _timer.Stop();
        _timer.Interval = (int)duration.TotalMilliseconds;
        _timer.Start();
    }

    public void StopTimer() => _timer.Stop();

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        AttachedCore.OnTimerElapsed();
    }

    // Clipboard (the Capture's image, the Copy's Transcript, the Insert's Transcript and its restored snapshot all go
    // through this one write)

    private const int ClipboardRetryTimes = 10;
    private const int ClipboardRetryDelayMilliseconds = 50;

    /// Copied onto the clipboard so it outlives this process's object, retried for about half a second while another
    /// app holds the clipboard; false when it still does.
    private static bool TrySetClipboard(DataObject data)
    {
        try
        {
            Clipboard.SetDataObject(data, copy: true, ClipboardRetryTimes, ClipboardRetryDelayMilliseconds);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    // Process

    public void Exit()
    {
        if (_ownsInstanceMutex)
        {
            // Wakes a replacing Instance now rather than when this process dies; the abandoned-mutex path in
            // WaitForInstanceMutex stays as the fallback for an Instance that never gets here.
            _instanceMutex!.ReleaseMutex();
            _ownsInstanceMutex = false;
        }

        _exited.Set();
        Application.ExitThread();
    }
}
