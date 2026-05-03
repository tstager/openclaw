---
title: Windows app stabilization sessions
summary: Stabilization plan for the native Windows app before redesign.
read_when:
  - Stabilizing the native Windows companion app before redesign work
  - Fixing Windows realtime, authentication, preferences, command, packaging, or layout issues
---

## Status

This plan follows the WinUI 3 code review completed before the Windows app
redesign. It is split into three coding sessions so each pass can be
implemented and verified independently.

## Session 1: Realtime Reliability And Lifetime

Goal: make the Windows app fail fast and cleanly when the gateway WebSocket
disconnects, returns malformed data, or the window closes.

- Add a bounded timeout for gateway realtime requests.
- Fault and clear pending realtime requests when the socket closes,
  disconnects, or receives malformed frames.
- Ensure `GatewayRealtimeClient` cleanup is deterministic through app/window
  lifetime disposal.
- Unsubscribe window event handlers on close.
- Add focused tests for request timeout and disconnect or malformed-frame
  pending cleanup.

Validation:

- Run the focused Windows realtime tests.
- Run targeted formatting or diff checks for touched files.

## Session 2: Authentication, Secure Storage, And Preferences

Goal: align Windows authentication and persisted state with the gateway protocol
and avoid writing sensitive tokens as plain JSON.

- Send signed device identity through the `device` connect parameter.
- Send persisted device tokens through `auth.deviceToken`, not `auth.token`.
- Store sensitive Windows companion tokens in Windows secure storage instead of
  plain preferences JSON.
- Make preference writes serialized and atomic so concurrent writes cannot
  corrupt the file.
- Add tests for auth payload shape and preference write durability.

Validation:

- Run focused Windows auth and preferences tests.
- Run protocol compatibility checks for the touched gateway contract path if edited.

## Session 3: Commands, Packaging, And Layout Hardening

Goal: remove common user-facing failure modes before applying the visual redesign.

- Replace fire-and-forget command execution with command state, busy gating, and
  visible error reporting.
- Resolve the MSIX/runtime dependency gap so the app can start or locate the
  gateway/CLI predictably on a clean Windows install.
- Add scrollable content containers for dashboard areas that can overflow.
- Keep UI changes minimal and structural; the broader visual redesign belongs to
  the design implementation plan.
- Add focused tests or manual validation for command error paths and packaged
  app startup assumptions.

Validation:

- Run focused command/controller tests.
- Build the Windows app when packaging or lazy/runtime boundaries change.
- Run a manual app launch smoke if packaging or startup paths change.

## Completion Criteria

- Each session lands as a small, reviewable change.
- The Windows app no longer silently hangs on gateway realtime failure paths.
- The app uses the protocol's device-auth contract before redesign work begins.
- Sensitive local tokens are no longer persisted in plain JSON preferences.
- The app shell has enough command and layout resilience to support the
  redesign safely.
