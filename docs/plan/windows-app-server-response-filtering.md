---
title: Windows app Sessions event visibility plan
summary: Plan for adding persisted client-side event visibility controls to the Sessions surface so each realtime event type can be shown or hidden without losing the ability to restore it.
read_when:
  - Planning event visibility controls for data returned from the OpenClaw server in the Windows companion app
  - Deciding whether Windows event filtering should stay app-local or extend gateway RPC contracts
  - Picking up follow-up work on Sessions realtime events in `apps/windows`
---

# Windows app Sessions event visibility plan

## Status

Scoped for a first pass that adds **persisted client-side event visibility
controls** to the Sessions surface in the Windows companion app. Saved for later
implementation; this document is the implementation-ready plan, not an active
work item.

## Problem

Add per-event visibility controls for the Sessions surface in the Windows
companion app so users can hide noisy realtime events such as `tick`, `health`,
and similar operational updates, while still being able to restore each hidden
event type to the view later. The first pass must not permanently discard
events just because the current view hides them.

## Current state

- The WinUI shell is built programmatically in
  `apps/windows/OpenClaw.Windows/MainWindow.cs`.
- `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs` currently fetches
  `chat.history` through `LoadChatHistoryAsync(...)` and forwards every incoming
  event frame to UI subscribers as `GatewayRealtimeEvent`.
- `apps/windows/OpenClaw.Windows/MainWindow.cs` currently appends every incoming
  realtime event to `chatRealtimeEvents` in `OnRealtimeEventReceived(...)`.
- The Sessions surface renders those items through `BuildChatEventRow(...)` as
  raw `event:<name> <payload>` text, with no event-type filtering today.
- The gateway already supports basic chat-history shaping through
  `chat.history` params (`sessionKey`, `limit`, `maxChars`) in
  `src/gateway/protocol/schema/logs-chat.ts`.
- The public gateway event set includes both chat-relevant and noisy
  operational events in `src/gateway/server-methods-list.ts`. Examples that can
  appear in the Sessions event stream include `chat`, `session.message`,
  `session.tool`, `tick`, `health`, and `heartbeat`.
- There is no existing gateway-side contract for filtering the realtime event
  stream for the Windows Sessions page, so the first pass should stay app-local.
- Persisted Windows settings live in
  `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs`. There is no current
  filter-preference model.
- Existing tests already cover gateway payload parsing and settings persistence:
  - `apps/windows/OpenClaw.Windows.Tests/GatewayRealtimeClientTests.cs`
  - `apps/windows/OpenClaw.Windows.Tests/AppPreferencesStoreTests.cs`

## Confirmed scope

- First-pass page: Sessions only
- Filtering strategy: client-side event visibility in the Windows app
- Persistence: save selected event-type visibility in Windows app preferences
  across restarts
- Goal state: users can show or hide each received gateway event type, including
  restoring hidden event types without reconnecting or reloading history
- Timing: save this plan for later implementation

## Recommendation

Implement **client-side event visibility inside the Windows app** and persist
Sessions visibility state in app-local preferences.

Why this is the right first pass:

- It fits the current gateway contracts.
- It avoids protocol churn for a Windows-only UX improvement.
- It keeps generated C# protocol code unchanged unless a shared contract benefit
  is clear.
- It lets the app iterate on event visibility UX before freezing RPC parameters.

## Draft implementation plan

## 1. Add Sessions event visibility state and defaults

- Introduce app-local records for Sessions event visibility. The model should
  store visibility by exact gateway event name, for example `chat`,
  `session.message`, `session.tool`, `tick`, `health`, and `heartbeat`.
- Seed the visibility model from the known gateway event list where practical,
  then add any newly observed event names at runtime so every displayed event
  type can be hidden and later restored.
- Keep raw chat history and raw realtime events in the current Sessions buffer
  independent from the selected visibility state. Changing a checkbox should
  only rederive the visible rows; it should not delete matching raw events.
- Use a bounded raw realtime event buffer so noisy hidden events do not grow
  memory usage forever. The first pass can cap by recent event count.
- Extend `AppPreferences` and `AppPreferencesStore` with defaults for missing
  persisted fields and tests for persisted Sessions event visibility.
- Prefer explicit event categories for presets and grouping, but keep the final
  show/hide decision keyed by exact event name so users retain per-event control.

## 2. Add session event classification and filtered presentation helpers

- Move filtering logic into small app-local helpers or presentation records
  instead of embedding the entire decision tree in `MainWindow.cs`.
- Keep the raw-to-filtered transformation explicit so refresh, reconnect, and
  notification logic remain predictable.
- Use payload metadata such as `sessionKey` when available so the Sessions page
  prefers events relevant to the active session. When `sessionKey` is absent,
  classify by event name and apply the per-event visibility state instead of
  dropping the event as irrelevant.
- Define a small event taxonomy for presets and UI grouping:
  - Chat transcript: `chat`, `session.message`
  - Tools and side results: `session.tool`, `chat.side_result`
  - Operational status: `tick`, `health`, `heartbeat`, `presence`,
    `sessions.changed`
  - Pairing and approvals: `device.pair.*`, `node.pair.*`, approval events
  - Other: any event name not in the known groups
- Make chat-only a preset that turns off every non-chat event type. It should
  not be a separate irreversible mode; users must be able to re-enable any event
  type afterward.
- Avoid rerendering the Sessions event list when a new hidden event arrives and
  does not change the visible rows. Home activity and notifications can still
  process the raw event normally.

## 3. Wire Sessions filter controls into the WinUI shell

- Add compact event visibility controls to the Sessions page using the existing
  programmatic WinUI pattern in `MainWindow.cs`.
- Use a checklist, menu, or compact flyout that exposes every known or observed
  event type with an independent checkbox.
- Include fast actions for common workflows:
  - Show all
  - Hide all operational events
  - Chat only
  - Reset to defaults
- Keep controls keyboard-accessible and consistent with current button, toggle,
  combo box, and text input usage.
- Update empty states so users can distinguish "no server data" from "no results
  match the current filters."
- Show a hidden-count or filtered-count cue when events exist in the raw buffer
  but the current visibility settings hide them.
- Persist changes as app preferences. Toggling a checkbox should update the
  rendered Sessions view immediately.

## 4. Add focused tests

- Windows tests for:
  - event classification
  - event visibility defaults
  - persisted Sessions filter preferences
  - filtered helper behavior
  - chat-only behavior
  - newly observed event names becoming available in the visibility list
  - toggling a hidden event type back on without reconnecting or refetching
  - filtered-out noisy events not changing visible rows
  - raw realtime event buffer limits
- Keep the first pass Windows-only; gateway tests are unnecessary unless a later
  change expands the RPC contracts.

## Validation

```powershell
pnpm windows:protocol:check
pnpm windows:build
pnpm windows:test
```
