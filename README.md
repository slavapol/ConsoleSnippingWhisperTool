# ConsoleSnippingTool

A Windows tray tool with no on-screen UI that does two jobs from a hotkey:

- **Capture**: press `Ctrl+Win+Z`, click two corners, and the rectangle between them is on the clipboard as PNG and bitmap. Sounds confirm each step.
- **Dictation**: hold `Ctrl+Win+X`, speak, release, and what you said lands on the clipboard, ready to paste wherever you like; with `"insert": true` it is pasted straight into the app that has keyboard focus instead. Or press `Ctrl+Win+A`, let the other people in a Teams or Zoom meeting talk, and press it again: what they said is on the clipboard. Transcription runs on OpenAI's API with your own key.

Everything is configured in one file, `config.json`, beside the exe.

## config.json

### Where it is and how it is read

- The file lives in the same folder as `ConsoleSnippingTool.exe`. If it is missing, the tool writes it with the defaults on first start, so the easiest way to begin is to run the tool once and edit the file it created.
- The file is read once at startup. Edits apply on the next launch: edit, then start the exe again. Starting a second copy replaces the running one, so there is nothing to close first.
- Every field falls back on its own. A missing, mistyped or out-of-range field takes its default, and each such field is reported once with a tray balloon and a line in the Console (tray menu, Show). The tool never refuses to start over configuration. If the whole file is not valid JSON, all defaults are used and one balloon says so.
- Comments (`//` and `/* */`) and trailing commas are tolerated, since the file is hand-edited. Unknown keys are ignored.
- The tool never writes to the file after creating it, so a key you put in it stays exactly where you put it.

### The default file

```json
{
  "hotkey": "Ctrl+Win+Z",
  "armedTimeoutSeconds": 30,
  "sounds": {
    "ready": null,
    "corner": null,
    "sent": null,
    "done": null
  },
  "openAi": {
    "apiKey": null
  },
  "dictation": {
    "hotkey": "Ctrl+Win+X",
    "listeningTimeoutSeconds": 120,
    "playbackHotkey": "Ctrl+Win+A",
    "playbackTimeoutSeconds": 600,
    "insert": false,
    "model": "gpt-transcribe",
    "languages": ["en", "ru"],
    "prompt": null,
    "cleanup": false,
    "cleanupModel": "gpt-6-luna"
  }
}
```

### Hotkey strings

All three hotkeys, `hotkey`, `dictation.hotkey` and `dictation.playbackHotkey`, use the same grammar: one or more modifiers and exactly one key, joined by `+`.

| Part | Accepted values |
| --- | --- |
| Modifiers (at least one) | `Ctrl` or `Control`, `Alt`, `Shift`, `Win` or `Windows` |
| Key (exactly one) | a letter `A`–`Z`, a digit `0`–`9`, or `F1`–`F24` |

Case and spaces around the parts do not matter: `" ctrl + shift + f5 "` is the same as `Ctrl+Shift+F5`. A string with no modifier, no key, two keys, or a key outside the list is invalid and falls back to the default. A hotkey already registered by another program is reported at startup; the tool keeps running and the other hotkeys still work.

### Capture settings

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `hotkey` | string | `"Ctrl+Win+Z"` | The hotkey that arms a Capture. Pressing it again while armed cancels. |
| `armedTimeoutSeconds` | integer, 1 to 3600 | `30` | How long the tool waits for a corner before cancelling on its own. The timer restarts after the first corner. |

### Sounds

Each entry under `sounds` is one of three values:

| Value | Effect |
| --- | --- |
| `null` | the built-in click, a 30 ms synthesised key click |
| `"none"` | silent |
| `"path\\to\\file.wav"` | your own sound; a relative path is resolved from the exe folder |

A file that is missing or is not a WAV falls back to the built-in click with a Console warning, so a bad path never makes the tool silent by surprise.

| Field | Plays when |
| --- | --- |
| `sounds.ready` | the tool starts reading your input: a Capture is armed, or the microphone or the playback device opened for a Dictation |
| `sounds.corner` | the first corner of a Capture was taken |
| `sounds.sent` | a Dictation's audio is on its way: you released the Microphone hotkey, pressed the Playback hotkey again, or the cap hit |
| `sounds.done` | the result landed: the image or the text is on the clipboard, or the text was pasted |

### OpenAI settings

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `openAi.apiKey` | string or `null` | `null` | The API key used for Dictation. `null` means "read the `OPENAI_API_KEY` environment variable". A value here wins over the environment variable. |

Without a key anywhere, the tool starts normally, Capture works, and each press of either Dictation hotkey shows a balloon saying what to set. The key is never written to the file by the tool, never logged and never shown in the Console.

### Dictation settings

`hotkey`, `listeningTimeoutSeconds` and `insert` are the Microphone's; `playbackHotkey` and `playbackTimeoutSeconds` are Playback's; `model`, `languages`, `prompt`, `cleanup` and `cleanupModel` apply to both, so transcription is configured once.

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `dictation.hotkey` | string | `"Ctrl+Win+X"` | The Microphone hotkey, held while you speak. Releasing any key of the chord ends Listening. A press shorter than half a second does nothing. Esc while holding discards the audio. If it is the same chord as the Capture hotkey in effect, it is reset to the default and reported. |
| `dictation.listeningTimeoutSeconds` | integer, 1 to 600 | `120` | The longest one Microphone Dictation can be. When the cap hits, the audio so far is transcribed and delivered as if you had released. |
| `dictation.playbackHotkey` | string | `"Ctrl+Win+A"` | The Playback hotkey: press it once to start Listening on what the computer plays, and again to send. A second press within half a second does nothing. Esc is not used. If it is the same chord as the Capture or the Microphone hotkey in effect, it is reset to the default and reported. |
| `dictation.playbackTimeoutSeconds` | integer, 1 to 600 | `600` | The longest one Playback Dictation can be. When the cap hits, the audio so far is transcribed and copied as if you had pressed again. Ten minutes of audio is about 19 MB, under OpenAI's 25 MB upload limit. |
| `dictation.insert` | `true` or `false` | `false` | Microphone only. `false` leaves the text on the clipboard as a normal copy: it replaces whatever was there, shows up in `Win+V` history, and you paste it where and when you like. `true` pastes it straight into the app that has keyboard focus at the moment you release, keeps it out of `Win+V`, and puts the previous clipboard contents back afterwards. A Playback Transcript is always copied, whatever this says. |
| `dictation.model` | non-empty string | `"gpt-transcribe"` | The OpenAI transcription model. Change it when OpenAI retires the default. |
| `dictation.languages` | array of strings, or `null` | `["en", "ru"]` | The languages you expect to speak or to hear in a meeting, as codes such as `"en"`, `"de"` or `"ru"`; a bilingual speaker lists both. `null` or `[]` lets the service detect freely. The codes are not checked at startup: an unknown one makes the service reject that Dictation, which is reported like any other failure. The Copied or Inserted Console line shows the languages the service actually detected. |
| `dictation.prompt` | string or `null` | `null` | Free text the service uses as a vocabulary and style hint: names, product terms, jargon, preferred spellings. |
| `dictation.cleanup` | `true` or `false` | `false` | When `true`, the transcript goes through a second, small language-model call that removes fillers and false starts and keeps only the speaker's final self-correction. It adds a fraction of a second per Dictation, more for a long Playback Dictation. |
| `dictation.cleanupModel` | non-empty string | `"gpt-6-luna"` | The model used for the cleanup pass. |

What the Dictation costs: one transcription call per Dictation at the model's per-minute price, plus one small text call when `cleanup` is on. A Playback Dictation is billed for the audio that played, up to ten minutes with the default cap. Nothing is charged while the tool sits idle.

## Examples

### Different hotkeys, and only the final sound

```json
{
  "hotkey": "Ctrl+Alt+S",
  "armedTimeoutSeconds": 15,
  "sounds": {
    "ready": "none",
    "corner": "none",
    "sent": "none",
    "done": null
  },
  "openAi": { "apiKey": null },
  "dictation": {
    "hotkey": "Ctrl+Alt+D",
    "listeningTimeoutSeconds": 120,
    "playbackHotkey": "Ctrl+Alt+M",
    "playbackTimeoutSeconds": 600,
    "insert": false,
    "model": "gpt-transcribe",
    "languages": ["en", "ru"],
    "prompt": null,
    "cleanup": false,
    "cleanupModel": "gpt-6-luna"
  }
}
```

### Dictation tuned for a bilingual developer

The key lives in the file, the expected languages are English and Russian so either works without choosing, the prompt teaches the service the names it keeps hearing, the cleanup pass is on, and `insert` is on so the text is pasted straight into the editor instead of waiting on the clipboard. A Playback Dictation still only copies, and it shares the languages, the prompt and the cleanup pass.

```json
{
  "hotkey": "Ctrl+Win+Z",
  "armedTimeoutSeconds": 30,
  "sounds": { "ready": null, "corner": null, "sent": null, "done": null },
  "openAi": {
    "apiKey": "sk-proj-...your key..."
  },
  "dictation": {
    "hotkey": "Ctrl+Win+X",
    "listeningTimeoutSeconds": 180,
    "playbackHotkey": "Ctrl+Win+A",
    "playbackTimeoutSeconds": 600,
    "insert": true,
    "model": "gpt-transcribe",
    "languages": ["en", "ru"],
    "prompt": "ConsoleSnippingTool, WASAPI, Win32, xUnit, Slava. Technical dictation about C# and Windows; keep code identifiers as spoken.",
    "cleanup": true,
    "cleanupModel": "gpt-6-luna"
  }
}
```

### English only, fastest possible, key from the environment

```json
{
  "hotkey": "Ctrl+Win+Z",
  "armedTimeoutSeconds": 30,
  "sounds": { "ready": null, "corner": null, "sent": null, "done": null },
  "openAi": { "apiKey": null },
  "dictation": {
    "hotkey": "Ctrl+Win+X",
    "listeningTimeoutSeconds": 60,
    "playbackHotkey": "Ctrl+Win+A",
    "playbackTimeoutSeconds": 600,
    "insert": false,
    "model": "gpt-transcribe",
    "languages": ["en"],
    "prompt": null,
    "cleanup": false,
    "cleanupModel": "gpt-6-luna"
  }
}
```

### Your own sounds

Put the files beside the exe, or anywhere and use a full path. Backslashes are doubled inside JSON strings; forward slashes also work.

```json
{
  "hotkey": "Ctrl+Win+Z",
  "armedTimeoutSeconds": 30,
  "sounds": {
    "ready": "sounds/ready.wav",
    "corner": "sounds/tick.wav",
    "sent": "C:\\Users\\me\\Sounds\\whoosh.wav",
    "done": "sounds/done.wav"
  },
  "openAi": { "apiKey": null },
  "dictation": {
    "hotkey": "Ctrl+Win+X",
    "listeningTimeoutSeconds": 120,
    "playbackHotkey": "Ctrl+Win+A",
    "playbackTimeoutSeconds": 600,
    "insert": false,
    "model": "gpt-transcribe",
    "languages": ["en", "ru"],
    "prompt": null,
    "cleanup": false,
    "cleanupModel": "gpt-6-luna"
  }
}
```

### Comments and trailing commas are fine

```json
{
  // Capture
  "hotkey": "Ctrl+Win+Z",
  "armedTimeoutSeconds": 30,
  "sounds": { "ready": null, "corner": null, "sent": null, "done": null, },
  // Dictation; the key comes from OPENAI_API_KEY
  "openAi": { "apiKey": null },
  "dictation": {
    "hotkey": "Ctrl+Win+X",
    "listeningTimeoutSeconds": 120,
    /* Playback: what the computer plays, such as a meeting */
    "playbackHotkey": "Ctrl+Win+A",
    "playbackTimeoutSeconds": 600,
    "insert": false,
    "model": "gpt-transcribe",
    "languages": ["en"],
    "prompt": null,
    "cleanup": false,
    "cleanupModel": "gpt-6-luna",
  },
}
```

### What a mistake looks like

With this file:

```json
{
  "hotkey": "Ctrl+Home",
  "armedTimeoutSeconds": 0,
  "dictation": {
    "hotkey": "Ctrl+Win+Z",
    "cleanup": "yes"
  }
}
```

the tool still starts, uses the defaults for the bad fields, and the Console shows lines like these, each also announced by a balloon:

```
config.json: hotkey "Ctrl+Home" is not a valid Hotkey; using default Ctrl+Win+Z
config.json: armedTimeoutSeconds 0 is not an integer from 1 to 3600; using default 30
config.json: sounds is missing; using default built-in click
config.json: openAi is missing; using defaults
config.json: dictation.hotkey Ctrl+Win+Z is the same as the Capture Hotkey in effect; using default Ctrl+Win+X
config.json: dictation.listeningTimeoutSeconds is missing; using default 120
config.json: dictation.playbackHotkey is missing; using default Ctrl+Win+A
config.json: dictation.playbackTimeoutSeconds is missing; using default 600
config.json: dictation.insert is missing; using default false
config.json: dictation.model is missing; using default gpt-transcribe
config.json: dictation.languages is missing; using default en, ru
config.json: dictation.prompt is missing; using default null
config.json: dictation.cleanup "yes" is not true or false; using default false
config.json: dictation.cleanupModel is missing; using default gpt-6-luna
```

Missing fields are reported too, so the quickest way to a quiet startup is to start from the default file and change only the values you care about.

## Where to look when something is off

The tray menu has Show, Hide and Exit. Show opens a Console that prints the three hotkeys, how to cancel, and one timestamped line per event: armed, corners, captured size, listening and the device being heard, how many seconds of audio were sent, how many characters were copied or pasted and how long the wait was, the languages the service detected, the transcript itself, and every fallback or failure with its reason.
