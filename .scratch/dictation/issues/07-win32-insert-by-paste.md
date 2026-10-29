# 07 Win32: Insert by clipboard paste with restore

Status: ready-for-agent
Blocked by: 01

Spec: `.scratch/dictation/spec.md` → Insert, user stories 3, 15 to 17, 33. ADR-0003. Notes: `insert-and-hooks.md` in the shared notes directory (section "Insert").

## Goal

`TryInsertText(text, out reason)` in `Win32Platform.Insert.cs` pastes the Transcript into the focused app and restores the clipboard afterwards, exactly as ADR-0003 describes. P/Invoke declarations (`SendInput`, `INPUT`/`KEYBDINPUT`, `GetAsyncKeyState`, `GetClipboardSequenceNumber`) go in a new `NativeMethods.Input.cs` (partial). No other file changes.

## Behaviour

1. Snapshot: copy every format of `Clipboard.GetDataObject()` into a fresh `DataObject`, skipping formats whose data cannot be read. Clipboard opens are retried for about half a second, as `TryCaptureToClipboard` already does; if the clipboard stays locked, return false with reason `clipboard locked by another app`.
2. Set a `DataObject` holding the text as Unicode text plus the three marker formats from the notes (`ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory` = 0, `CanUploadToCloudClipboard` = 0), with `copy: true`; record `GetClipboardSequenceNumber()`.
3. Wait up to 2 s, polling every 20 ms with `GetAsyncKeyState`, for both Ctrl, Alt, Shift and Win keys to be up. Still down → restore the snapshot now and return false with reason `modifier keys still held`.
4. `SendInput` with Ctrl down, V down, V up, Ctrl up in one call. Fewer than four events accepted → restore now and return false with reason `input blocked`.
5. Schedule the restore 300 ms later on a `System.Windows.Forms.Timer`: `SetDataObject(snapshot, copy: true)` unless the sequence number changed, in which case skip. Return true immediately after scheduling.

- Keep every `Clipboard` call on the message-loop thread (the only STA thread); the wait in step 3 may block that thread for up to 2 s, which is acceptable because the only thing it delays is the paste itself.
- An empty snapshot (nothing on the clipboard before) restores to an empty clipboard.

## Tests

None at the unit level: clipboard and input injection. Record in Comments what a debug run showed: a paste into Notepad, into a browser text box and into Windows Terminal; a snapshot containing an image (take a Capture first) coming back after the paste; the behaviour when Ctrl is held through the paste.

## Acceptance

`dotnet build` 0 warnings; `dotnet test` green (unchanged count); `IPlatform` unchanged.

## Comments
