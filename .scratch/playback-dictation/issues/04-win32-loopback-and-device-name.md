# 04 Win32 loopback capture and device name

Status: resolved
Blocked by: 01

Spec: `.scratch/playback-dictation/spec.md` → Playback capture; ADR-0005; user stories 7, 8, 25 to 28. Prior art: `.scratch/dictation/issues/05-win32-wasapi-microphone.md` and the existing WASAPI capture in the adapter's Audio folder.

## Goal

`TryOpenSource(Playback)` records the Default Communication Device's render endpoint by WASAPI loopback at 16 kHz mono, and `TryOpenSource` reports the endpoint's friendly name for both Sources.

## Scope

- Parameterise the existing capture by Source: Microphone stays `eCapture`, `eCommunications`, today's flags; Playback is `eRender`, `eCommunications`, today's flags plus `AUDCLNT_STREAMFLAGS_LOOPBACK`. Same format, buffer, drain thread and silent-packet handling. The drain thread's name says which Source it drains.
- Friendly name: `IMMDevice.OpenPropertyStore(STGM_READ)`, then `IPropertyStore.GetValue(PKEY_Device_FriendlyName)` as a `PROPVARIANT` holding `VT_LPWSTR`, copied to a string and released with `PropVariantClear`. Any failure gives an empty name and never fails the open. Declare `IPropertyStore` with the source generator next to the existing interfaces, every method in vtable order.
- Values to verify against the Windows SDK headers before use: `AUDCLNT_STREAMFLAGS_LOOPBACK` 0x00020000 (audiosessiontypes.h), `eRender` 0 (mmdeviceapi.h), `STGM_READ` 0, `IPropertyStore` IID 886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99 (propsys.h), `PKEY_Device_FriendlyName` {A45C254E-DF1C-4EFD-8020-67D146A850E0}, 14 (functiondiscoverykeys_devpkey.h), `VT_LPWSTR` 31; `PROPVARIANT` is 24 bytes on x64 with the pointer at offset 8.
- Files: the adapter's Audio folder and the adapter's Source partial from ticket 01. Do not touch any Core file or any test outside what this ticket adds.

## Acceptance

`dotnet build` 0 warnings, `dotnet test` green. A subagent with a desktop session runs a throwaway check, not committed, that opens Playback while a sound plays and reports the clip length and both device names; without one it says so and leaves that to ticket 05's manual checklist. Record in Comments whether `Initialize` accepted the loopback flags with `AUTOCONVERTPCM` on this PC.

## Comments

### Implemented (2026-10-30)

`dotnet build` 0 warnings, `dotnet test` 203 green; no test added or changed.

**Built**

- Every value in Scope matches the 10.0.26100 SDK headers. `STGM_READ` is in coml2api.h and `PROPERTYKEY` in wtypes.h. PROPVARIANT's C layout is the VARTYPE plus three WORD pads, then the union at offset 8. Its largest arms are 16 bytes (BLOB, the counted arrays, the overlaid DECIMAL), so it is 24 bytes on x64. `IPropertyStore`'s vtable is GetCount, GetAt, GetValue, SetValue, Commit.
- `Audio/WasapiInterfaces.cs`:
  - adds `IPropertyStore`, `PropertyKey` (20 bytes) and `PropVariant` (explicit layout: `Type` at 0, `Pointer` at 8, size 24);
  - `IMMDevice.OpenPropertyStore` is now `[PreserveSig] int`.
- `Audio/NativeMethods.Audio.cs` adds `eRender`, `AUDCLNT_STREAMFLAGS_LOOPBACK`, `STGM_READ`, `VT_LPWSTR`, `PKEY_Device_FriendlyName` and `PropVariantClear` (ole32).
- `WasapiCapture.TryOpen(DictationSource source, int sampleRate)`:
  - The Microphone opens `eCapture`. Playback opens `eRender` with `LOOPBACK` added to the shared flags. Both use `eCommunications`, and everything else is unchanged.
  - The drain thread is named `Microphone drain` or `Playback drain`.
  - `DeviceName` is read from the property store after `GetDefaultAudioEndpoint`. It is `""` on any failure and never fails the open.
- `Win32Platform.Source.cs`: `TryOpenSource` opens either Source. It reports `DeviceName`, or `""` when the open fails.

**Not tested, and why.** The capture, the loopback and the property store are adapter code (spec, Testing Decisions). The only pure piece is the per-Source table of data flow and flags, and a test of it would only restate the SDK constants. The test project cannot see the internal adapter types, so the struct sizes were checked in the throwaway run instead.

**Throwaway check** (a scratchpad console harness compiled from `Win32/Audio`, STA, not committed). The console and communications render defaults are both `Speakers (EMEET OfficeCore M0 Plus)`.

| Run | Clip | Non-zero samples | Peak |
| --- | --- | --- | --- |
| Microphone, 0.5 s open (named `Microphone (EMEET OfficeCore M0 Plus)`) | 0.50 s | 1841 | 1 |
| Playback, 2 s open, a 1 s 440 Hz tone at amplitude 1000 | 1.41 s | 16113 | 1000 |
| Playback, 1.5 s open, only the built-in click (run twice, identical) | 0.44 s | 607 | 2887 |
| Playback, 1 s open, nothing played | 0 bytes | — | — |

- `Initialize` accepted `LOOPBACK | AUTOCONVERTPCM | SRC_DEFAULT_QUALITY | EVENTCALLBACK` at 16 kHz mono on this PC. The tone came back at its played peak. The device names on both Sources were read.
- The player keeps rendering about 0.4 s of zeros after each sound. So a 30 ms Ready click alone gives a 0.44 s clip, 60 ms under the 500 ms "Nothing heard" threshold. A custom Ready sound longer than about 60 ms would make a silent Playback clip pass that threshold and be sent.

**Deviations**

1. The device name is a `DeviceName` property on the capture, not an `out` parameter of `TryOpen`.
2. `OpenPropertyStore` became `[PreserveSig]` so that a failure is a value, as for every method already called.

- Resolved: branch tip 542ad88 fast-forwarded onto integration/playback-dictation; build 0 warnings, 203 tests green on integration. The 0.44 s click clip moved the spec's "nothing played" threshold from 500 ms to 1 s (spec and ticket 03 updated).
