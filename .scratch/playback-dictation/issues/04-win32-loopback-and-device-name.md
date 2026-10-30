# 04 Win32 loopback capture and device name

Status: ready-for-agent
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
