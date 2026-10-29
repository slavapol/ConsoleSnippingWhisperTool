# 06 Win32: OpenAI upload transport, warm-up and Cleanup call

Status: ready-for-agent
Blocked by: 01

Spec: `.scratch/dictation/spec.md` → Transcription request, Cleanup, user stories 6, 19, 30, 31, 38. ADR-0004. Notes: `openai-api.md` in the shared notes directory (endpoints, field names, model names, error shape; verify field names against the live docs linked there before relying on them).

## Goal

`WarmUpService`, `BeginTranscription` and `BeginCleanup` in `Win32Platform.Service.cs` talk to OpenAI over one `HttpClient` and hand results back to the core on the message loop through `MessageWindow.Post`. The upload transport sits behind a small transport interface so a streaming one can be added later. The JSON building and parsing is pure code in its own type and is unit tested. Files: `Win32Platform.Service.cs`, new files under `Win32/OpenAi/` (the transport interface, the upload transport, the cleanup client, a public static `OpenAiJson` helper for request bodies and response parsing), and a new `OpenAiJsonTests.cs`. No other file changes; no packages.

## Behaviour

- One static `HttpClient` over `SocketsHttpHandler`, base address `https://api.openai.com/`, no default timeout (per-request 20 s via a `CancellationTokenSource`).
- `WarmUpService(apiKey)`: fire-and-forget `GET v1/models` with the bearer header; swallow every outcome.
- `BeginTranscription(sequence, clip, request)`: on the thread pool, build the WAV with `Wav.FromPcm16` when ticket 05 has merged, otherwise with a local copy of the same 44-byte header (resolve to `Wav` in ticket 08 if needed); `POST v1/audio/transcriptions` multipart: `file` as `dictation.wav` (`audio/wav`), `model`, `response_format=json`, `prompt` when set, `languages[]` when `Language` is set. Success → `Post(() => core.OnTranscript(sequence, text))`. Any failure → `Post(() => core.OnTranscriptionFailed(sequence, reason))`.
- `BeginCleanup(sequence, transcript, request)`: `POST v1/responses` with `model`, `instructions`, `input` (the transcript as a string) and `reasoning: { "effort": "none" }`; the output text is the concatenation of every `output[].content[].text` whose `type` is `output_text`. Success → `OnCleanedTranscript`; failure → `OnCleanupFailed`.
- Failure reason: for an HTTP error, `"<status code> <first line of error.message>"`, falling back to `"<status code> <reason phrase>"` when the body is not the error shape; for exceptions, `"timed out after 20 s"` for the cancellation case and the exception message otherwise. Reasons never contain the key.
- The bearer header is set per request; the key is held only in the request records passed in.

## Tests (OpenAiJsonTests.cs)

Transcript parsing: `{"text":"hello"}` → `hello`; missing `text` → throws or returns a reason (pick one and test it). Error parsing: the error body shape → `401 Incorrect API key provided`; a non-JSON body → the status and reason phrase. Cleanup body: contains `model`, `instructions`, `input`, `reasoning.effort = none`. Cleanup output parsing: two `output_text` parts are concatenated; a refusal-only output yields empty. Multipart assembly: the form contains `model`, `response_format`, `languages[]` only when set, `prompt` only when set, and the file part named `file` with filename `dictation.wav`.

## Acceptance

`dotnet build` 0 warnings; `dotnet test` green; `IPlatform` unchanged; no network access in tests.

## Comments
