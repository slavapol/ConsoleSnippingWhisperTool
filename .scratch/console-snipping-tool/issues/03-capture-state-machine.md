# 03 Capture state machine

Status: ready-for-agent
Blocked by: 02

Spec: `.scratch/console-snipping-tool/spec.md` → Core and Platform seam (transitions), Region, Hotkey (Esc), user stories 9–29. ADR-0002.

## Goal

The whole Capture flow in `SnipperCore.Capture.cs`, test-first through `FakePlatform`. Edit only that file, `Region`, and a new test file `CaptureTests.cs`; do not touch `IPlatform`.

## Behaviour

- Idle + `OnHotkey` → `InstallMouseHook`, `RegisterCancelKey`, `StartArmedTimer(config timeout)`, `PlaySound(Ready)`, state Armed, log "Armed".
- Armed + `OnLeftButtonDown(x,y)` → remember Corner, `StartArmedTimer` again (restart), `PlaySound(Corner)`, state FirstCorner, log "Corner 1 at x,y"; return true (swallowed).
- FirstCorner + `OnLeftButtonDown(x,y)` → `Region.FromCorners(a, b)`: inclusive bounding rectangle, order-independent, minimum 1×1; then `RemoveMouseHook`, `UnregisterCancelKey`, `StopArmedTimer`, state Idle, `TryCaptureToClipboard(region)`; on true `PlaySound(Done)` and log "Captured W×H → clipboard"; on false no sound, `ShowBalloon` and log the failure. Return true.
- Idle + `OnLeftButtonDown` → return false (not swallowed). Same for `OnRightButtonDown` while Idle.
- Armed/FirstCorner + `OnCancelKey` / `OnRightButtonDown` / `OnHotkey` / `OnTimerElapsed` → `Cancel(reason)`: `RemoveMouseHook`, `UnregisterCancelKey`, `StopArmedTimer`, state Idle, no sound, log "Cancelled (reason)" or "Timed out". `OnRightButtonDown` returns true in those states.
- `OnCancelKey` and `OnTimerElapsed` while Idle do nothing.
- `OnExit` while Armed cancels first (ticket 02 left the hook; make sure the real cancel runs).

## Tests (CaptureTests.cs)

Arm sequence and order of calls; corners (10,10) and (5,20) → `TryCaptureToClipboard(5,10,6x11)`; corners in the opposite order give the same Region; same point twice → 1×1; corners on different monitors (negative coordinates) work; each cancel path removes hook, releases Esc, stops timer, plays nothing; timer restarts after first Corner; clipboard failure → no Done, balloon; clicks while Idle are not swallowed; Exit while FirstCorner cancels before unregistering the Hotkey.

## Acceptance

`dotnet test` green; `IPlatform` unchanged.

## Comments
