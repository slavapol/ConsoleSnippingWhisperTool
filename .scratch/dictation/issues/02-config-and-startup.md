# 02 Dictation configuration, API key resolution and startup registration

Status: ready-for-agent
Blocked by: 01

Spec: `.scratch/dictation/spec.md` → Configuration, Console and tray, Hotkey (registration and the identical-Hotkeys rule), user stories 21 to 29, 34, 36 to 39, 42. Prior art: `.scratch/console-snipping-tool/issues/04-hotkey-and-config.md` and the existing `Config.Load`.

## Goal

`config.json` carries the new fields with the same per-field fallback as today, the default file written on first start matches the spec's JSON exactly, startup resolves the API key, registers the Dictation Hotkey, and the tooltip and Console header show both Hotkeys. Files: `Config.cs`, `SnipperCore.Startup.cs`, `SnipperCore.Console.cs`, `SnipperCore.cs` (a field for the resolved key), the test project's `ConfigJson.cs` builder and the config, startup and console test files. Do not touch `SnipperCore.Capture.cs`, `SnipperCore.Dictation.cs` or any Win32 file.

## Behaviour

- `Config.ToJson()` writes the spec's default file: the existing fields, `sounds` gaining `sent` between `corner` and `done`, then `openAi` with `apiKey`, then `dictation` with `hotkey`, `listeningTimeoutSeconds`, `model`, `language`, `prompt`, `cleanup`, `cleanupModel`, in that order.
- `Config.Load` rules, each with the existing `Missing`/`Invalid` message shape and one problem per field: `sounds.sent` like the other sounds; `openAi` must be an object (missing or wrong type: one problem, default); `openAi.apiKey` null or a non-empty string; `dictation` must be an object (missing or wrong type: one problem for the whole object, all defaults); `dictation.hotkey` the Hotkey grammar; `dictation.listeningTimeoutSeconds` integer 1 to 600; `dictation.model` and `dictation.cleanupModel` non-empty strings; `dictation.language` null or non-empty string; `dictation.prompt` null or string; `dictation.cleanup` boolean. A `dictation.hotkey` equal to `hotkey` (same modifiers and key, after both parsed) is a problem `config.json: dictation.hotkey <text> is the same as hotkey; using default Ctrl+Win+X`, reported once, and the Dictation one falls back.
- Startup order in `Start()`: acquire instance, load config, resolve API key, set up sounds, register Capture Hotkey, register Dictation Hotkey, set up tray.
- Resolve API key: `Config.OpenAiApiKey` if set, else `ReadEnvironmentVariable("OPENAI_API_KEY")`; a null or whitespace result means no key. The resolved key is a private field of the core, never logged, never written to config. No key: `Report("Dictation needs an API key: set openAi.apiKey in config.json or the OPENAI_API_KEY environment variable")`.
- Register Dictation Hotkey: `RegisterDictationHotkey(config.DictationHotkey)`; on false `Report($"Hotkey {text} is already taken by another app; edit config.json and relaunch")`, keep running.
- Tooltip: `ConsoleSnippingTool (capture Ctrl+Win+Z, dictate Ctrl+Win+X)` from the configured texts.
- Console header on Show: `ConsoleSnippingTool`, `Press Ctrl+Win+Z to capture`, `Hold Ctrl+Win+X to dictate`, `Esc or right-click cancels a capture; Esc discards a dictation`, blank line, then the ring.
- `OnExit` also calls `UnregisterDictationHotkey()` right after `UnregisterHotkey()`.
- `ConfigJson.With(...)` in the test project gains parameters for every new field with the defaults, so a test states only the field it is about, and its output equals `new Config().ToJson()`.

## Tests

Default file round-trips (`ConfigJson.With()` equals `new Config().ToJson()`, and `Load` of it yields no problems). Each new field: valid value honoured; missing reported with the default; wrong type reported; `listeningTimeoutSeconds` 0, 601 and `"long"` fall back; `cleanup: "yes"` falls back; `dictation` as an array gives one problem and all defaults; identical Hotkeys give one problem and the Dictation default. Startup: key from config → no report and `WarmUpService` not yet called (that is ticket 03); key from environment → `ReadEnvironmentVariable(OPENAI_API_KEY)` recorded and no report; no key anywhere → the exact balloon; the key never appears in any `Calls` entry; Dictation Hotkey registered after the Capture one; taken Dictation Hotkey → balloon, tray still set; tooltip text; header lines; Exit unregisters both Hotkeys in order.

## Acceptance

`dotnet test` green; `IPlatform` unchanged from ticket 01.

## Comments
