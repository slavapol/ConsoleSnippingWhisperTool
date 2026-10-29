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
- Built on branch `worktree-agent-a0f7f6d7c7ae11e53`: `Win32Platform.Insert.cs` runs the five Behaviour steps in the listed order, and the new `NativeMethods.Input.cs` holds `SendInput`, `INPUT`/`KEYBDINPUT` (with `MOUSEINPUT` and `HARDWAREINPUT` in the union so `cbSize` is the real 40 bytes on x64), `GetAsyncKeyState`, `GetClipboardSequenceNumber` and `VK_V`; `VK_CONTROL` and the eight left and right modifier codes the Insert polls are ticket 04's in `NativeMethods.Keyboard.cs`, and this file's own copies were dropped when the merge of `integration/dictation` made them duplicates; the `INPUT`/`KEYBDINPUT` layout, the meaning of the `SendInput` return value and the spelling of the three marker formats were checked against Microsoft Learn. `dotnet build` 0 warnings, `dotnet test` 88 green with none added (clipboard and input injection sit below the Platform seam). The paste was not exercised: the desktop was live, so no debug run was made and the Notepad, browser, Windows Terminal, image-snapshot and held-Ctrl observations this ticket asks for are still open for the end-to-end ticket. Deviations from the text: the snapshot read goes through `Clipboard.GetDataObject()`, whose retry is WinForms' own 10 x 100 ms (about a second, not configurable) rather than the half second the writes use; formats are read and re-set with `autoConvert: false`, so the snapshot holds the names actually on the clipboard (including the ones Windows synthesises) in the shapes WinForms writes natively, where the default conversion would add `FileNameW`/`System.String` entries whose restore goes through the removed BinaryFormatter path; the restore timer is created per Insert and disposed in its own Tick instead of living in a field; the immediate restores on the `modifier keys still held` and `input blocked` paths apply the same sequence-number guard as the delayed one; a restore that still finds the clipboard locked is dropped silently, since the core is Idle by then and the Transcript simply stays on the clipboard. Observation only: waiting for the modifiers before touching the clipboard would spare the restore on the modifier path, but the order written in the ticket was kept.
