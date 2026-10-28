# 04 Hotkey grammar and configuration loading

Status: resolved
Blocked by: 02

Spec: `.scratch/console-snipping-tool/spec.md` → Hotkey, Configuration, user stories 36–45.

## Goal

`config.json` is parsed with per-field fallback, and the Hotkey string grammar is implemented. Tested only through the seam: `FakePlatform.ConfigText` in, Platform calls out. Edit `Hotkey` (add parsing), `Config` (add loading), `SnipperCore.Startup.cs` (the config step only), and a new test file `ConfigTests.cs`. Do not touch `IPlatform` or the Capture/Console files.

## Behaviour

- `Hotkey.TryParse(string text, out Hotkey hotkey)`: tokens split on `+`, trimmed, case-insensitive. Modifiers: `Ctrl`/`Control`, `Alt`, `Shift`, `Win`/`Windows`. Key: a single letter `A`–`Z`, a digit `0`–`9`, or `F1`–`F24`, mapped to its VK code. Exactly one key and at least one modifier; anything else is invalid. `Text` is normalised to canonical form: modifiers in the order Ctrl, Alt, Shift, Win, then the key upper-cased, joined by `+`.
- `Config.Load(string? json)` returns the config plus a list of problems (one message per field that fell back, or one message for unparseable JSON). Fields and rules as in the spec: `hotkey`, `armedTimeoutSeconds` (positive integer), `sounds.ready|corner|done` (`null` → BuiltIn, `"none"` → None, any other string → File). Unknown keys are ignored. Serialising the default config produces the exact default file written in ticket 02.
- `Start()` config step: when text is non-null, load it; for every problem, `ShowBalloon` and `Log` naming the field. Registered Hotkey, tray tooltip and Armed timer duration come from the loaded config.

## Tests (ConfigTests.cs)

`"Ctrl+Shift+F5"` registers Ctrl|Shift, VK_F5 and tooltip shows `Ctrl+Shift+F5`; `" ctrl + win + z "` normalises; `"Z"` (no modifier), `"Ctrl"` (no key), `"Ctrl+Z+X"` (two keys), `"Ctrl+Home"` (unsupported key) each fall back to the default with a balloon; `armedTimeoutSeconds: 0`, `-3`, `"ten"` fall back to 30 with a balloon while a valid hotkey in the same file is still honoured; unparseable JSON → all defaults, one balloon; `sounds.done: "none"` → `TrySetSound(Done, None)`; `sounds.ready: "click.wav"` → `TrySetSound(Ready, File(click.wav))`; a timeout of 12 → `StartArmedTimer(12s)` on arming (requires ticket 03 merged; if it isn't yet, assert via the config object instead and leave a comment).

## Acceptance

`dotnet test` green; `IPlatform` unchanged.

## Comments
- Resolved in 581542d (04: Hotkey grammar and config.json loading with per-field fallback), merged onto integration/console-snipping-tool after ticket 03; build green, 60 tests green on integration. Missing fields are reported too (per spec), so an empty object yields three balloons. Message shape: `config.json: <field> <raw value> is not ...; using default <value>`; unparseable: `config.json could not be parsed (line N); using defaults`. Config.FileName = "config.json". Relative sound paths pass through verbatim for the adapter to resolve.
