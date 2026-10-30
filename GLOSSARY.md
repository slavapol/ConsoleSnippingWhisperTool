# ConsoleSnippingTool

A Windows tray tool with no on-screen UI that does two jobs from a hotkey: copies a user-chosen rectangle of the screen to the clipboard, and puts what the user says into the app they are typing in.

## Language

### Capturing

**Capture**:
One complete run of the tool's first job: hotkey, two corners, image on the clipboard.
_Avoid_: Snip, screenshot (the word for the resulting image, not the act), grab

**Armed**:
The state between the Capture Hotkey press and the second corner, during which the tool waits for corners and swallows the user's left clicks.
_Avoid_: Capture mode, snipping mode, active

**Corner**:
The screen position of a left-button press while Armed. Two corners define a Region.
_Avoid_: Point, click position

**Region**:
The rectangle spanned by two Corners, inclusive of both corner pixels, in virtual-screen coordinates. The order of the corners does not matter.
_Avoid_: Selection, area, rectangle, bounds

**Cancel**:
Leaving Armed without a Capture: by Esc, right-click, the Hotkey again, or the Armed timeout.
_Avoid_: Abort, escape

### Dictating

**Dictation**:
One complete run of the tool's second job: Hotkey held, speech, text in the focused app.
_Avoid_: Voice-to-text, voice mode, WispFlow mode, recording

**Listening**:
The state between the Dictation Hotkey press and its Release, during which the microphone is open.
_Avoid_: Recording, dictation mode, active

**Release**:
The first key of the held Dictation Hotkey going up. It ends Listening; the Listening cap counts as a Release.
_Avoid_: Key-up, stop

**Tap**:
A Dictation Hotkey held for less than half a second. It ends Listening with nothing sent.
_Avoid_: Short press, accidental press

**Transcribing**:
The state between the Release and the Transcript arriving, during which the audio is with the Service.
_Avoid_: Processing, uploading, waiting

**Transcript**:
The text the Service returned for one Dictation, in the language it was spoken. Never a translation.
_Avoid_: Transcription (the act), translated text, result, output

**Cleanup**:
An optional second pass that rewrites a Transcript to drop fillers and false starts and keep only the speaker's final self-correction.
_Avoid_: Smart formatting, polish, post-processing

**Insert**:
Delivering the Transcript into the app that has keyboard focus. One of the two ways a Dictation ends; the other is Copy.
_Avoid_: Paste (the mechanism, not the act), type, inject, editor insert

**Copy**:
Leaving the Transcript on the clipboard for the user to paste wherever and whenever they choose. The default way a Dictation ends.
_Avoid_: Clipboard mode, clipboard only

**Discard**:
Leaving Listening without a Transcript: by Esc, a Tap, or Exit.
_Avoid_: Cancel (the Capture word), abort

**Service**:
The remote speech-to-text API that turns the audio of a Dictation into a Transcript.
_Avoid_: Backend, OpenAI (the vendor, not the role), the model

### Signals

**Ready signal**:
The sound that tells the user the tool has started reading their input: Armed for a Capture, Listening for a Dictation.
_Avoid_: Start sound, beep

**Corner signal**:
The sound that confirms the first Corner was registered.

**Sent signal**:
The sound that confirms the Release was registered and the audio is on its way to the Service.
_Avoid_: Stop sound, release sound

**Done signal**:
The sound that tells the user the job's result has landed: the Region or the Transcript on the clipboard, or the Transcript in the app.
_Avoid_: Finish sound, success sound

### Shell

**Hotkey**:
A global key combination that starts a job: pressed, the Capture Hotkey arms the tool; held, the Dictation Hotkey opens Listening. Written as modifiers plus one key, for example `Ctrl+Win+Z`.
_Avoid_: Shortcut, keybinding, accelerator

**Console**:
The on-demand text window that shows the Hotkeys and a log of Captures and Dictations. It exists only between Show and Hide.
_Avoid_: Main window, log window, terminal

**Show / Hide**:
The tray commands that open and close the Console. The tray icon itself is always present.
_Avoid_: Open/close, minimize/restore

**Instance**:
One running process of the tool. Only one Instance exists at a time; a newly started Instance replaces the running one.
