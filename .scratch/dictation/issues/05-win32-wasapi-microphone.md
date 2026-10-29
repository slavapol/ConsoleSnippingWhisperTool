# 05 Win32: microphone capture through hand-written WASAPI, shared WAV writer

Status: ready-for-agent
Blocked by: 01

Spec: `.scratch/dictation/spec.md` → Microphone, user stories 8, 32, 39. Notes: `wasapi-capture.md` in the shared notes directory (interfaces, GUIDs, vtable order, flags, the COM source-generator recipe).

## Goal

`TryOpenMicrophone()` and `CloseMicrophone()` in `Win32Platform.Microphone.cs` capture 16 kHz 16-bit mono PCM from the Default Communication Device through WASAPI shared mode with no package, and the WAV container writer that `ClickSound` already contains is extracted into a core `Wav` type that both the click and the microphone clip use. Files: `Win32Platform.Microphone.cs`, new files under `Win32/Audio/` for the COM interfaces and the capture loop, new `Core/Wav.cs`, `Core/ClickSound.cs` (use `Wav`), the app csproj (`AllowUnsafeBlocks`, needed by the COM source generator), and a new `WavTests.cs`. No other file changes.

## Behaviour

- `Wav.FromPcm16(byte[] pcm16, int sampleRate, short channels)` returns a complete RIFF/WAVE byte array (fmt chunk: PCM, channels, rate, byte rate, block align, 16 bits; data chunk). `ClickSound.Wav()` builds its samples as today and hands them to `Wav`; its bytes must not change (assert against the current output in a test, for example its length and the fmt fields).
- `TryOpenMicrophone`: create the device enumerator, `GetDefaultAudioEndpoint(eCapture, eCommunications)`, activate `IAudioClient`, `Initialize` shared with `EVENTCALLBACK | AUTOCONVERTPCM | SRC_DEFAULT_QUALITY` and the 16 kHz mono format (the rate is a constant in one place so a later transport can change it), set the event handle, get `IAudioCaptureClient`, `Start`, and run a background thread that waits on the event and drains packets into a `MemoryStream` (silent packets written as zeros). Any failure (no device, `E_NOTFOUND`, an HRESULT from `Initialize`) releases what was created and returns false. The device is opened per call; nothing is cached between Dictations.
- `CloseMicrophone`: signal the thread to stop, `Stop` the client, join the thread, release every COM object and the event, and return `new AudioClip(bytes, 16000)`. Calling it when nothing is open returns an empty clip.
- COM interfaces as `[GeneratedComInterface]` partial interfaces with `[Guid]`, methods in vtable order, unused slots declared with plain signatures; the enumerator created with `CoCreateInstance` and wrapped with `StrategyBasedComWrappers`. Use `[PreserveSig]` HRESULT returns where a failure is expected (`GetDefaultAudioEndpoint`, `Initialize`) so `E_NOTFOUND` is a return value, not an exception.
- Everything that touches the first `IAudioClient` call happens on the thread that calls `TryOpenMicrophone` (the message loop, STA); only the drain loop runs on the worker.

## Tests

`WavTests.cs`: header fields for a known buffer (RIFF size, fmt values, data size); an empty buffer gives a 44-byte file; `ClickSound.Wav()` still starts with `RIFF`, is 44 bytes plus its sample count times two, and declares 44100 Hz mono 16-bit. The WASAPI code is not unit tested; record in Comments whether a debug-run recording was listened to or at least produced a non-empty, non-silent clip.

## Acceptance

`dotnet build` 0 warnings (the generator may need `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`); `dotnet test` green; `IPlatform` unchanged; no new package references.

## Comments
