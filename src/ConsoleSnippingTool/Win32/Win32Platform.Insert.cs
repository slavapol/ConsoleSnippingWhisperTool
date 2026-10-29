using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ConsoleSnippingTool.Win32;

public sealed partial class Win32Platform
{
    // The registered clipboard formats Windows reads (Microsoft Learn, "Clipboard Formats", checked 2026-10-29): any
    // data under the first keeps the item out of both clipboard history and cloud sync; a DWORD 0 under each of the
    // other two does the same for one of them. All three go on so a Windows that knows only some still obeys.
    private const string ExcludeClipboardContentFromMonitorProcessing = "ExcludeClipboardContentFromMonitorProcessing";
    private const string CanIncludeInClipboardHistory = "CanIncludeInClipboardHistory";
    private const string CanUploadToCloudClipboard = "CanUploadToCloudClipboard";
    private static readonly byte[] DwordZero = new byte[4];

    private const int ClipboardRetryTimes = 10;
    private const int ClipboardRetryDelayMilliseconds = 50;
    private const int ModifierWaitMilliseconds = 2000;
    private const int ModifierPollMilliseconds = 20;
    private const int RestoreDelayMilliseconds = 300;

    private const string ClipboardLockedReason = "clipboard locked by another app";

    // Held physically, any of these turns the synthesised Ctrl+V into another shortcut in the target app.
    private static readonly int[] ModifierKeys =
    {
        NativeMethods.VK_LCONTROL, NativeMethods.VK_RCONTROL,
        NativeMethods.VK_LMENU, NativeMethods.VK_RMENU,
        NativeMethods.VK_LSHIFT, NativeMethods.VK_RSHIFT,
        NativeMethods.VK_LWIN, NativeMethods.VK_RWIN,
    };

    /// Insert by paste with restore (ADR-0003): snapshot the clipboard, put the Transcript on it, wait for the physical
    /// modifiers to be up, send Ctrl+V, and put the snapshot back 300 ms later. Runs on the message-loop thread, the
    /// only STA thread, which every Clipboard call needs; the modifier wait may hold it for up to 2 s, and the only
    /// thing that delays is the paste itself. True means the paste was sent, not that the target consumed it: a
    /// window of higher integrity receives nothing and Windows reports no error.
    public bool TryInsertText(string text, out string reason)
    {
        if (!TrySnapshotClipboard(out var snapshot) || !TrySetClipboard(TranscriptDataObject(text)))
        {
            reason = ClipboardLockedReason;
            return false;
        }

        var sequenceAfterTranscript = NativeMethods.GetClipboardSequenceNumber();

        if (!WaitForModifiersUp())
        {
            RestoreClipboard(snapshot, sequenceAfterTranscript);
            reason = "modifier keys still held";
            return false;
        }

        if (!SendCtrlV())
        {
            RestoreClipboard(snapshot, sequenceAfterTranscript);
            reason = "input blocked";
            return false;
        }

        ScheduleRestore(snapshot, sequenceAfterTranscript);
        reason = "";
        return true;
    }

    // Snapshot

    /// Copies every format on the clipboard into a DataObject of our own. The clipboard's IDataObject may be a proxy
    /// into the owning app, so each format is read now, before the Transcript replaces it; a format whose data cannot
    /// be read (the owner fails to render it, or it is a serialised object WinForms no longer deserialises) is left
    /// out rather than failing the Insert. Formats are taken without conversion on either side: the names are the
    /// ones actually on the clipboard, including those Windows synthesises, and each comes back as the one shape
    /// WinForms writes natively (text as string, a bitmap as Bitmap, a file list as string[], the rest as raw bytes).
    /// False only when the clipboard cannot be opened; GetDataObject retries that for about a second on its own.
    private static bool TrySnapshotClipboard([NotNullWhen(true)] out DataObject? snapshot)
    {
        IDataObject? source;
        string[] formats;
        try
        {
            source = Clipboard.GetDataObject();
            formats = source?.GetFormats(autoConvert: false) ?? [];
        }
        catch (ExternalException)
        {
            snapshot = null;
            return false;
        }

        snapshot = new DataObject();
        foreach (var format in formats)
        {
            object? data;
            try
            {
                data = source!.GetData(format, autoConvert: false);
            }
            catch (Exception)
            {
                // The owner's COM error, WinForms' NotSupportedException for a serialised object, or whatever else one
                // format does: that format is skipped and the others still go into the snapshot.
                continue;
            }

            if (data is not null)
            {
                snapshot.SetData(format, autoConvert: false, data);
            }
        }

        return true;
    }

    // Transcript

    /// The Transcript as the one format a paste reads (Windows synthesises the ANSI text formats from it), flagged so
    /// that Win+V and cloud sync leave it alone. A Stream is written to the clipboard byte for byte, as the Capture's
    /// PNG already is, so a 4-byte stream of zeros is the serialised DWORD 0 the markers ask for.
    private static DataObject TranscriptDataObject(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, autoConvert: false, text);
        data.SetData(ExcludeClipboardContentFromMonitorProcessing, autoConvert: false, new MemoryStream(DwordZero, writable: false));
        data.SetData(CanIncludeInClipboardHistory, autoConvert: false, new MemoryStream(DwordZero, writable: false));
        data.SetData(CanUploadToCloudClipboard, autoConvert: false, new MemoryStream(DwordZero, writable: false));
        return data;
    }

    /// The same write as the Capture's: copied onto the clipboard so it outlives this process's object, retried for
    /// about half a second while another app holds the clipboard.
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

    // Modifiers and the paste

    /// Polls the physical state of both Ctrl, Alt, Shift and Win keys every 20 ms until all are up, or gives up after
    /// 2 s. GetAsyncKeyState answers zero (up) when it cannot see the foreground thread, which is the elevated-target
    /// case: the paste then goes ahead and is lost, as ADR-0003 documents.
    private static bool WaitForModifiersUp()
    {
        var waited = Stopwatch.StartNew();
        while (AnyModifierDown())
        {
            if (waited.ElapsedMilliseconds >= ModifierWaitMilliseconds)
            {
                return false;
            }

            Thread.Sleep(ModifierPollMilliseconds);
        }

        return true;
    }

    private static bool AnyModifierDown()
    {
        foreach (var key in ModifierKeys)
        {
            if (NativeMethods.GetAsyncKeyState(key) < 0)
            {
                return true;
            }
        }

        return false;
    }

    /// Ctrl down, V down, V up, Ctrl up in one SendInput call, so no other input lands between them. Fewer than four
    /// events inserted means the input was blocked.
    private static bool SendCtrlV()
    {
        var inputs = new[]
        {
            KeyEvent(NativeMethods.VK_CONTROL, down: true),
            KeyEvent(NativeMethods.VK_V, down: true),
            KeyEvent(NativeMethods.VK_V, down: false),
            KeyEvent(NativeMethods.VK_CONTROL, down: false),
        };
        var inserted = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        return inserted == inputs.Length;
    }

    private static NativeMethods.INPUT KeyEvent(ushort virtualKey, bool down) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.INPUTUNION
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = virtualKey,
                dwFlags = down ? 0 : NativeMethods.KEYEVENTF_KEYUP,
            },
        },
    };

    // Restore

    /// The target reads the clipboard when it handles the paste, some time after SendInput returns, so the restore
    /// waits 300 ms (a guess, ADR-0003) on a message-loop timer of its own: the shared one-shot may already be timing
    /// the next Dictation. One timer per Insert, carrying its own snapshot, so nothing is shared between Inserts.
    private static void ScheduleRestore(DataObject snapshot, uint sequenceAfterTranscript)
    {
        var timer = new System.Windows.Forms.Timer { Interval = RestoreDelayMilliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            RestoreClipboard(snapshot, sequenceAfterTranscript);
        };
        timer.Start();
    }

    /// Puts the snapshot back, unless the sequence number shows another writer since the Transcript went on: then the
    /// clipboard is theirs and is left alone. An empty snapshot (nothing was on the clipboard) puts an empty data
    /// object back, leaving the clipboard empty. A clipboard still locked after the retries is let go: the core is
    /// Idle by now and has nobody to report to, and the Transcript stays on the clipboard.
    private static void RestoreClipboard(DataObject snapshot, uint sequenceAfterTranscript)
    {
        if (NativeMethods.GetClipboardSequenceNumber() != sequenceAfterTranscript)
        {
            return;
        }

        TrySetClipboard(snapshot);
    }
}
