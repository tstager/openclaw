---
summary: Multi-session implementation plan for the native OpenClaw Windows app redesign.
title: Windows app redesign sessions
read_when:
  - Implementing the native Windows companion app redesign
  - Planning follow-up WinUI navigation, dashboard, settings, chat, pairing, approvals, logs, tray, or notification work
  - Picking up Windows app redesign work across multiple coding sessions
---

# Windows App Redesign Sessions

## Summary

This plan breaks the native Windows companion app redesign into implementation
sessions that can be completed independently. The redesign should finish the
main WinUI shell first, then add tray functionality after the app has reusable
state, command, navigation, and activity surfaces.

Use `docs/plan/windows-app-design.md` as the design target. Use
`../Openclaw-windows-node` only as a reference for tray lifecycle, menu
structure, deep links, notifications, activity history, autostart, and quick
actions. Do not copy the reference app wholesale.

## Current Status

- Session 1 is implemented in `apps/windows/OpenClaw.Windows/MainWindow.cs`.
- The current shell has a WinUI `NavigationView` with Home, Sessions,
  Approvals, Pairing, Devices, Logs, and pinned Settings destinations.
- `pnpm windows:protocol:check`, `pnpm windows:build`, and
  `pnpm windows:test` passed after session 1.

## Session 2: Home Dashboard

Build Home into the first operational dashboard instead of leaving it as a
temporary Status page.

- Replace the temporary Home content with Gateway status, onboarding health,
  connection state, recent activity placeholders, and compact quick actions.
- Keep Install, Start, Restart, Stop, Connect, and Open Logs behavior wired to
  the existing services.
- Present status as structured rows or cards instead of raw multiline text.
- Keep Gateway configuration ownership in existing Gateway and preference
  services.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

## Session 3: Shared State And Commands

Extract reusable state and command boundaries from `MainWindow.cs` without
changing behavior.

- Move Gateway action routing, status refresh state, log path state, and common
  command wrappers behind app-local service or view-model boundaries.
- Keep existing UI controls working while making command execution reusable by
  future tray actions.
- Preserve error reporting, crash logging, realtime state updates, and
  preference persistence semantics.
- Avoid adding tray code in this session; only prepare the boundaries that tray
  will call later.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

## Session 4: Sessions And Chat

Turn the Sessions destination into the native chat/session workspace.

- Replace the temporary Chat page with a conversation-focused layout.
- Keep current chat history and send behavior intact.
- Add clear empty, disconnected, sending, failed, and connected states.
- Keep the composer and refresh/send actions keyboard-accessible.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

## Session 5: Approvals And Pairing

Redesign approvals and pairing as focused operator workflows.

- Replace raw approval rows with approval cards or compact list rows.
- Replace raw pairing rows with pairing request cards or compact list rows.
- Surface pending approvals and pairing readiness from Home when the data is
  available.
- Keep allow, deny, approve, reject, and refresh behavior unchanged.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

## Session 6: Devices And Capabilities

Redesign the Devices destination around Windows capability cards.

- Show screen capture, camera, microphone, hotkey, notifications, and overlay
  status as capability cards.
- Preserve current device capability tests, toggle saving, notification, and
  overlay behavior.
- Show permission state, last action result, and repair guidance in the card
  body.
- Do not add Windows node-mode capabilities in this redesign phase.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

Manual smoke:

- Launch the app.
- Run screen, record, camera, notify, and overlay actions from Devices.
- Save voice and global hotkey toggles, relaunch, and confirm persistence.

## Session 7: Logs And Diagnostics

Make Logs a useful destination instead of only a reveal-file action.

- Show app and Gateway log locations, last error, last refresh time, and current
  Gateway status.
- Add refresh, copy path, reveal file, and open folder actions where the current
  services expose the necessary data.
- Keep raw logs readable in light and dark themes.
- Do not add support bundle or full diagnostics parity yet.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
git diff --check -- docs/plan/windows-app-redesign-sessions.md
```

## Session 8: Settings Redesign

Replace the flat Settings destination with grouped Windows settings sections.

- Group settings into Gateway Connection, Identity, Startup, Notifications,
  Devices, Storage and Logs, and About.
- Preserve secure token storage, Gateway URL, Gateway token, chat session,
  voice controls, and global hotkey persistence.
- Add reserved settings rows for future tray preferences such as autostart,
  minimize to tray, notification preferences, and tray quick actions.
- Keep app-local settings app-local; delegate Gateway configuration changes to
  existing Gateway flows.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

## Session 9: Tray Foundation

Add the initial tray host after the redesigned app shell and shared command
boundaries are stable.

- Use the `../Openclaw-windows-node` tray app as a lifecycle reference for
  tray icon behavior, single-instance activation, keep-alive window handling,
  menu positioning, and quick actions.
- Initial tray menu should expose Gateway state, Open App, Install, Start,
  Restart, Stop, Connect, Open Logs, Settings, and Exit.
- Reuse the app-local command/state services from session 3.
- Do not add node mode, command palette, SSH tunnel, updater, or support bundle
  parity in this session.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

Manual smoke:

- Launch the app.
- Confirm the tray icon appears and survives showing/hiding the main window.
- Run each tray Gateway action.
- Open Home, Logs, and Settings from the tray menu.
- Exit from the tray menu and confirm the process shuts down cleanly.

## Session 10: Tray Notifications And Deep Links

Add actionable tray notifications and destination routing after the tray
foundation works.

- Add notification preferences for approvals, pairing requests, Gateway health,
  and device permission failures.
- Add a lightweight notification history or activity list that Home and tray
  can both reference.
- Add app-local deep-link route constants for Home, Sessions, Logs, Settings,
  Pairing, and diagnostics-style surfaces.
- Keep notification content concise and never include secrets.

Verification:

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```

Manual smoke:

- Trigger a test notification from Devices.
- Open the app from notification or tray action.
- Confirm deep links route to the expected destination.
- Confirm notification preferences persist after relaunch.

## Deferred Scope

- Windows node-mode capabilities.
- Command palette parity.
- SSH tunnel management.
- Updater parity.
- Full support bundle and diagnostics parity.
- Protocol changes that only serve Windows-specific UI.

## General Acceptance Criteria

- The first screen remains Home.
- All current functional surfaces remain reachable.
- Gateway config remains Gateway-owned.
- Windows UI changes do not hand-edit generated protocol code.
- Light and dark themes remain readable.
- Keyboard navigation works across navigation, command actions, settings,
  device controls, and chat.
- Each session leaves the Windows build and tests passing before commit.
