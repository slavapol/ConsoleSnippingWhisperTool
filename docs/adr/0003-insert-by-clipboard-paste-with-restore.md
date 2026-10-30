# Transcripts are inserted by a clipboard paste, not by synthesised keystrokes

Status: since the Copy-by-default change of 2026-10-29 this decision governs Insert only, which runs when `dictation.insert` is true. By default a Dictation ends in Copy, an ordinary clipboard write that replaces the previous contents, image included.

The obvious way to make dictated text appear at the caret is `SendInput` with `KEYEVENTF_UNICODE`, which Windows documents for voice recognition and which leaves the clipboard alone. The tool instead snapshots the clipboard, puts the Transcript on it flagged as excluded from clipboard history and cloud sync, sends Ctrl+V, and restores the snapshot 300 ms later. A newline typed as Enter sends the message in Slack, Teams and every chat box, while a pasted newline never does; a paste lands a 500-character Transcript at once instead of at the target's message-loop speed; and a key event synthesised while Ctrl or Win is still physically held reads as a shortcut in the target app. Wispr Flow and FreeFlow made the same choice.

## Consequences

- The clipboard is borrowed for about a third of a second. The restore delay is a guess: a target that reads the clipboard later than that pastes the restored contents. The restore is skipped if the clipboard sequence number shows someone else wrote in between.
- A Capture's image survives an Insert because WinForms data objects round-trip images, text, HTML and file lists; the snapshot is copied format by format before the Transcript goes on.
- Before sending Ctrl+V the tool waits, bounded, for every physical modifier to be up, because Win+Ctrl+V and Shift+Ctrl+V mean other things.
- Clipboard managers that ignore the exclusion formats will still record the Transcript.
- Neither a paste nor synthesised keystrokes reach a window of higher integrity from a non-elevated process, and Windows reports no error. A paste into an elevated console does nothing while the Done signal still plays. Elevated targets are unsupported.
- Apps that bind Ctrl+V to something other than paste receive that instead. Windows Terminal and the console host paste on Ctrl+V by default.
