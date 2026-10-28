# 06 Synthesised click and sound setup

Status: resolved
Blocked by: 04

Spec: `.scratch/console-snipping-tool/spec.md` → Sounds, user stories 40–42, 46–47.

## Goal

The built-in key-click exists as bytes, and the sound setup step of `Start()` handles fallback with a warning. Edit a new `ClickSound` type, the sound step in `SnipperCore.Startup.cs`, and a new test file `SoundTests.cs`. Do not touch `IPlatform`.

## Behaviour

- `ClickSound.Wav()` returns a `byte[]` holding a complete RIFF/WAVE file: 16-bit mono PCM at 44.1 kHz, about 30 ms long: white noise multiplied by a fast exponential decay (e.g. time constant ≈ 6 ms), peak amplitude around 0.6 of full scale, deterministic (seeded noise) so the bytes are stable. The Win32 adapter (ticket 07) plays these bytes for `SoundSource.BuiltIn`.
- `Start()` sound step: for each signal call `TrySetSound(signal, configured source)`; when it returns false, log "Sound for <signal> could not be loaded from <path>; using built-in click" and call `TrySetSound(signal, BuiltIn)`. `None` never fails.

## Tests (SoundTests.cs)

The WAV header is valid (RIFF, WAVE, fmt chunk with channels=1, rate=44100, bits=16, data chunk length matches); duration is between 20 and 40 ms; samples decay (RMS of the last quarter is below 10 % of the first quarter); two calls return identical bytes. Through the seam: a File source that fails to load leads to the fallback call and a log line; a File source that loads does not; a None source is set once.

## Acceptance

`dotnet test` green; `IPlatform` unchanged.

## Comments
- Resolved in a65ac5c (06: synthesised click and sound setup tests; branch tip 8943fa8 merges 05), merged onto integration/console-snipping-tool; build green, 78 tests green on integration. ClickSound.Wav(): 16-bit mono 44.1 kHz, 30 ms, exp decay tau 6 ms, peak 0.6, xorshift32 seed 0x5EEDC11C, 2,690 bytes, deterministic. The Startup sound step needed no change (ticket 02 already matched the wording). For ticket 08: wrap ClickSound.Wav() in a MemoryStream per SoundPlayer; the adapter must never return false for BuiltIn or None.
