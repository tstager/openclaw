---
title: Windows app pre-session 9 review findings
summary: WinUI 3 code review findings to fix before implementing tray foundation.
read_when:
  - Preparing to implement Windows app redesign session 9
  - Fixing native Windows tray lifecycle, launch preferences, or realtime sends
  - Reviewing WinUI 3 readiness before tray foundation work
---

# Windows App Pre-Session 9 Review Findings

## Summary

Fix these findings before implementing session 9 tray foundation. They were
found in the WinUI 3 review after session 8 and before tray work.

## Findings

## Resolution Status

Resolved in session 9 preparation:

- Normal window close now hides the WinUI shell when the tray host is active;
  explicit Exit performs teardown.
- Startup loads preferences before showing the shell and honors
  `OpenMainWindowOnLaunch`.
- Gateway realtime writes are serialized with a send gate and covered by a
  concurrent request test.

### 1. Main Window Close Tears Down The Tray Shell

Severity: High

The app currently disposes the tray host when the main window closes, and the
window `Closed` handler tears down realtime, hotkey, and overlay state. This
blocks tray-resident behavior because a normal window close removes the tray
icon and destroys the shell that session 9 needs.

Relevant code:

- `apps/windows/OpenClaw.Windows/App.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`

Expected fix:

- Split normal window close from explicit app exit.
- Normal close should hide or minimize the window to the tray once tray mode is
  enabled.
- Explicit Exit should perform the real tray, realtime, hotkey, and overlay
  teardown.

### 2. Launch Ignores OpenMainWindowOnLaunch

Severity: Medium

`OpenMainWindowOnLaunch` is exposed and persisted, but launch currently always
activates the main window. Preferences are loaded later by the window refresh
flow, so the setting cannot control startup behavior yet.

Relevant code:

- `apps/windows/OpenClaw.Windows/App.cs`
- `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`

Expected fix:

- Load app preferences early enough during startup to honor
  `OpenMainWindowOnLaunch`.
- Keep preference ownership in the existing app-local preferences store.
- Avoid moving Gateway configuration ownership into the app.

### 3. GatewayRealtimeClient Allows Concurrent WebSocket Sends

Severity: Medium

`GatewayRealtimeClient.RequestAsync` allows independent callers to send over the
same `ClientWebSocket` concurrently. The .NET `ClientWebSocket.SendAsync`
contract supports one send and one receive in parallel; multiple concurrent
sends are unsupported.

Relevant code:

- `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs`

Expected fix:

- Add a send gate around Gateway WebSocket writes.
- Preserve the existing receive loop and pending-request semantics.
- Add focused tests for concurrent request send serialization if practical.

Reference:

- https://learn.microsoft.com/dotnet/api/system.net.websockets.clientwebsocket.sendasync

## Verification For The Fix Pass

Run these before moving into session 9:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
git diff --check -- docs/plan/windows-app-pre-session-9-review.md
```
