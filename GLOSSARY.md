# ConsoleSnippingTool

A Windows tray tool that copies a user-chosen rectangle of the screen to the clipboard with no on-screen UI: a hotkey, two clicks, two sounds.

## Language

### Capturing

**Capture**:
One complete run of the tool's job: hotkey, two corners, image on the clipboard.
_Avoid_: Snip, screenshot (the word for the resulting image, not the act), grab

**Armed**:
The state between the hotkey press and the second corner, during which the tool waits for corners and swallows the user's left clicks.
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

### Signals

**Ready signal**:
The sound that tells the user the tool is Armed.
_Avoid_: Start sound, beep

**Corner signal**:
The sound that confirms the first Corner was registered.

**Done signal**:
The sound that tells the user the Region is on the clipboard.
_Avoid_: Finish sound, success sound

### Shell

**Hotkey**:
The global key combination that arms the tool. Written as modifiers plus one key, for example `Ctrl+Win+Z`.
_Avoid_: Shortcut, keybinding, accelerator

**Console**:
The on-demand text window that shows the Hotkey and a log of Captures. It exists only between Show and Hide.
_Avoid_: Main window, log window, terminal

**Show / Hide**:
The tray commands that open and close the Console. The tray icon itself is always present.
_Avoid_: Open/close, minimize/restore

**Instance**:
One running process of the tool. Only one Instance exists at a time; a newly started Instance replaces the running one.
