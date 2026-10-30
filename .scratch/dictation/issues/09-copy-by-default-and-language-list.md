# 09 Copy by default, and a list of expected languages

Status: ready-for-agent
Blocked by: 08

Spec: `.scratch/dictation/spec.md` → Delivery (Insert and Copy), Configuration, Transcription request, user stories 3, 4, 15 to 18, 26, 35 and the new 47 to 50. Glossary: `GLOSSARY.md` terms Copy, Insert, Transcript, Done signal. Agreed in a grilling session on 2026-10-29 (every recommended answer accepted).

## Goal

A Dictation ends by default with the Transcript on the clipboard (Copy), and only pastes into the focused app (Insert) when `dictation.insert` is `true`. The expected languages become a list, `dictation.languages`, default `["en", "ru"]`, sent to the service as repeated `languages[]` parts, and the languages the service detected are shown in the result log line. The README documents both. This ticket may change the seam (`IPlatform`, `FakePlatform`), unlike tickets 02 to 08.

## Behaviour

Configuration (`Config.cs`, `ConfigJson.cs`, the default file):

- `dictation.insert`: boolean, default `false`. Written in the default file right after `listeningTimeoutSeconds`.
- `dictation.languages`: replaces `dictation.language`. A JSON array of non-empty strings, default `["en", "ru"]`, written after `model`. `null` or `[]` means no hint is sent. Any other shape, or an array holding anything but non-empty strings, falls back to the default list with one report whose fallback text renders the list as `en, ru`. The codes themselves are not validated; the service rejects an unknown one at Dictation time. The old `language` key is not read any more and, like any unknown key, is ignored silently.
- Per-field fallback, messages and ordering follow the existing rules; `ConfigJson.With` gains `insert` and `languages` (the latter as a JSON literal string such as `["en", "ru"]`) and loses `language`.

Seam (`IPlatform`, `FakePlatform`, request records):

- `TranscriptionRequest(string ApiKey, string Model, IReadOnlyList<string> Languages, string? Prompt)`; its `ToString()` keeps omitting the key and prints the languages comma-separated. The fake records `BeginTranscription(seq:1,clip:3s,model:gpt-transcribe,languages:en|ru,prompt:null)` (codes joined by `|`, `none` when empty).
- `OnTranscript(int sequence, string text, IReadOnlyList<string> detectedLanguages)`: the languages the service reported, possibly empty. `OnCleanedTranscript` is unchanged; the core remembers the detected languages from `OnTranscript`.
- New `bool TryCopyText(string text)`: an ordinary Unicode-text clipboard write through the shared half-second retry, no history or cloud markers, no snapshot, no restore; false when the clipboard stayed locked. The fake records `TryCopyText(<text verbatim>)` and has a `CopyResult` knob, default true.

Core (`SnipperCore.Dictation.cs`):

- Where the core used to Insert (after `OnTranscript` with Cleanup off, after `OnCleanedTranscript`, after `OnCleanupFailed`), it now Delivers: Insert when `Config.Insert` is true, else Copy.
- Copy: `TryCopyText(text)` true means `PlaySound(Done)` and log `Copied 57 chars in 0.9 s [ru]: <text>`; false means `Report("Clipboard refused the Transcript; nothing was copied")`. Idle either way.
- Insert is unchanged except that its log line gains the same language suffix: `Inserted 57 chars in 0.9 s [en, ru]: <text>`. The suffix is ` [codes joined by ", "]` and is omitted when the service reported none.
- `BuildTranscriptionRequest` passes `Config.Languages`.

Adapter (`Win32/OpenAi/*`, `Win32Platform.Insert.cs` or a better-named partial):

- `OpenAiJson.TranscriptionForm` takes the list and adds one `languages[]` part per code, none for an empty list.
- `OpenAiJson.ParseTranscript` returns the text and the detected codes from the response's `languages[].code` (empty when the field is absent or empty); the upload transport posts both.
- `TryCopyText` in the adapter reuses `TrySetClipboard` with a plain `DataObject` holding Unicode text.

README (`README.md`): the Dictation table gains `insert` and replaces `language` with `languages`; the default file, every example and the "what a mistake looks like" block are updated to the real messages; the intro sentence about Dictation says the text lands on the clipboard by default and can be pasted into the focused app with `insert: true`.

## Tests

Config: `insert` true, false, missing, `"yes"`; `languages` valid two codes, one code, `[]`, `null`, a string, an array with a number, an array with an empty string, missing. Default file round-trips. Request: `LastTranscriptionRequest.Languages` carries the configured list; `OpenAiJsonTests` cover two `languages[]` parts, none for empty, and parsing `languages` present, absent and empty. Core (`DictationTests`): Copy happy path call order (`TryCopyText`, `PlaySound(Done)`, log with the detected codes), Copy with the clipboard refused (balloon, no Done), Cleanup on ends in Copy, `insert: true` still ends in Insert with the suffix on its log line, no suffix when the service reported nothing, Nothing heard unchanged.

## Acceptance

`dotnet build` 0 warnings; `dotnet test` green with every pre-existing test kept or adapted; README matches the code's messages; `GLOSSARY.md` already carries Copy.

## Comments
