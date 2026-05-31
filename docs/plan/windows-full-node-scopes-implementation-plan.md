# Windows Full Node And Scope Support Plan

## Current State

- The active Windows branch is `codex/native-windows-foundation-shell`, now consolidated with `copilot/native-windows-foundation-shell`.
- The current implemented feature set is documented in `docs/plan/windows-companion-software-manual.md`.
- The Windows companion is currently an operator client, not a second gateway.
- The Windows companion has tray lifetime, gateway controls, chat, sessions, approvals, pairing, logs, gateway event filtering, local diagnostics, activity history, notification history, support artifacts, SSH tunnel settings, guided onboarding, devices, Canvas/A2UI, theming, accent color, custom app palettes, and operational UX.
- The Devices page already exposes local Windows capability actions for primary-screen screenshot, bounded screen recording as PNG frames, camera still capture, notification test, overlay test, local Windows speech clip export, browser proxy readiness, microphone preference, and global hotkey preference.
- The Windows operator socket currently requests `operator.read`, `operator.write`, `operator.approvals`, and `operator.pairing`.
- The current gateway protocol defines the full operator scope set as `operator.read`, `operator.write`, `operator.admin`, `operator.approvals`, `operator.pairing`, and `operator.talk.secrets`.
- The current Canvas node is enabled by `Settings > Devices > Enable Canvas and A2UI node`, connects separately from the operator socket, and currently uses the macOS client id as a compatibility workaround.
- The remaining full-node work should reuse the implemented local Windows capability services rather than building new capture, notification, speech, overlay, or tunnel subsystems.

## Design Direction

Windows should become a dual-role companion:

- An operator UI socket with all requested operator scopes.
- A node capability socket with declared capabilities, commands, and permissions.

Operator scopes belong to `role: "operator"`. Node capabilities belong to `role: "node"` through `caps`, `commands`, and `permissions`. The implementation should not try to put operator scopes on the node connection.

## Session 1: Gateway Client Identity And Full Operator Scopes

Goal: make `openclaw-windows` a first-class gateway client id and let the Windows operator socket request the complete operator scope set.

Tasks:

- Verify gateway schema accepts `openclaw-windows` for `mode: "ui"` and `mode: "node"`.
- Add missing gateway tests if only UI coverage exists.
- Update `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs` to use `openclaw-windows`.
- Expand requested scopes to:
  - `operator.read`
  - `operator.write`
  - `operator.admin`
  - `operator.approvals`
  - `operator.pairing`
  - `operator.talk.secrets`
- Update Windows tests that currently assert `openclaw-macos`.
- Keep capability display honest: show `admin_capable` when `operator.admin` is granted, otherwise expose missing requested scopes in Home/Settings.

Verification:

- `pnpm windows:test`
- Focused gateway protocol/auth tests for client id validation.

## Session 2: Split Canvas Node Transport Into A General Windows Node

Goal: turn `WindowsCanvasNodeClient` into a general node transport while preserving current A2UI behavior.

Tasks:

- Introduce a general Windows node command registry.
- Keep Canvas/A2UI commands as handlers instead of transport-owned behavior.
- Preserve the existing Canvas page, `Connect Canvas`, `Refresh A2UI`, WebView2 host, trusted A2UI URL checks, and plugin surface refresh flow.
- Preserve current commands:
  - `canvas.present`
  - `canvas.hide`
  - `canvas.navigate`
  - `canvas.eval`
  - `canvas.snapshot`
  - `canvas.a2ui.push`
  - `canvas.a2ui.pushJSONL`
  - `canvas.a2ui.reset`
- Keep existing plugin surface refresh behavior.

Verification:

- Existing Canvas/A2UI tests pass.
- VM A2UI push still renders.

## Session 3: Advertise Native Windows Node Capabilities

Goal: make the Windows node declare the actual host surface from existing native services.

Tasks:

- Build node `caps` from `WindowsHostCapabilityProbe` and the existing app preferences.
- Build node `commands` from enabled handlers and already implemented device actions.
- Include permissions for:
  - screen capture and bounded screen recording
  - camera still capture
  - microphone or voice controls
  - Canvas/A2UI
  - notifications and overlays only if the gateway protocol accepts those as node claims
- Use current native services:
  - `WindowsDeviceCapabilityService`
  - `WindowsSystemTextToSpeechService`
  - `WindowsBrowserProxyCapabilityService`
  - `WindowsHostCapabilityProbe`
- Do not duplicate gateway configuration ownership or reimplement channel/model/provider auth in the app.

Verification:

- Unit tests assert connect payload caps/commands/permissions.
- Disabled preferences remove commands from the effective node surface.

## Session 4: Implement Native Node Invoke Handlers

Goal: expose real Windows device commands through `node.invoke`.

Tasks:

- Add invoke handlers that wrap the already implemented screenshot and bounded screen recording actions.
- Add an invoke handler that wraps the already implemented camera still capture action.
- Return structured success payloads with file metadata.
- Return structured failures for missing devices, denied permissions, invalid params, and unavailable handlers.
- Keep handler timeouts below the gateway invoke deadline.
- Preserve the local Devices page behavior; node invocation should call the same service layer, not fork a second implementation.

Verification:

- Unit tests for command dispatch, params, success payloads, and failure payloads.
- VM smoke for screen/camera where hardware and permissions allow.

## Session 5: Add Secure System Run Support

Goal: support `system.which`, `system.run.prepare`, and `system.run` only behind explicit node pairing and local policy.

Tasks:

- Add Windows command execution policy/preferences.
- Require local enablement before advertising system execution commands.
- Emit `exec.finished` and `exec.denied` events with `runId` and `sessionKey`.
- Return `node.invoke.result` for all system execution paths.
- Match gateway expectations from `src/node-host/invoke-system-run.ts`.

Verification:

- Unit tests for denied, prepared, successful, timed out, and failed command runs.
- Gateway tests prove `system.run` node pairing requires `operator.pairing` plus `operator.admin`.

## Session 6: Pairing And Scope Upgrade UX

Goal: make full access state obvious and repairable in the app.

Tasks:

- Show granted operator scopes.
- Show missing requested operator scopes.
- Show node connected/paired state.
- Explain when node approval needs admin because the node advertises system execution commands.
- Add a repair/re-request access flow for stale narrow device identity.
- Integrate this into the implemented Home, Pairing, Devices, Logs, and Settings pages rather than adding a new page.
- Surface the same node/pairing state in the tray flyout, reading the existing snapshot rather than building a parallel feed:
  - Add per-node topology rows to `TrayFlyoutComposer` (this Windows node plus any remote nodes the gateway reports), each with an online/role line and a platform badge, mirroring the `openclaw-windows-node` reference flyout.
  - Add a node-paired / connected-clients badge to the Gateway status row from the same connected-clients/pairing state this session surfaces.
  - Decide the header master-toggle semantics here: the reference flyout's top-right switch is the Windows-node enablement master, so wire it to the node enable/disable preference rather than treating it as chrome.
  - Extend `WindowsTraySnapshot` with the node/client fields these rows need; keep the composer purely a projection of that snapshot.
- Keep the manual's deployment model intact: gateway remains the source of truth and the Windows companion remains an operator UX plus Windows-native capability host.

Verification:

- Windows tests for narrow-scope repair.
- `TrayFlyoutComposer` tests assert per-node rows and the node-paired/client badge for representative snapshots.
- VM test with gateway token and approved pairing.

## Session 7: Gateway Compatibility And Documentation

Goal: document and prove Windows as a first-class node/operator client.

Tasks:

- Update protocol docs if gateway behavior or Windows support wording changes.
- Update `docs/plan/windows-companion-software-manual.md` with full-node setup and pairing steps after implementation lands.
- Add gateway tests for `openclaw-windows` UI and node mode if missing.

Verification:

- `pnpm windows:protocol:check`
- `pnpm windows:test`
- Focused gateway tests for protocol client id, device auth, node pairing, and node invoke.

## Session 8: VM Live Smoke

Goal: prove the end-to-end Windows full-node path.

Tasks:

- Launch gateway from current repo or current stable install as appropriate for the test.
- Launch the Windows app from repo build.
- Verify operator connection grants expected scopes.
- Verify Windows node appears in `openclaw nodes status`.
- Verify the tray flyout shows the Windows node row and the node-paired badge once pairing is approved.
- Verify Canvas/A2UI push renders.
- Verify screen/camera commands work or return clear permission errors.
- Verify `system.run` is blocked before admin-approved node pairing and succeeds within policy after approval.

Verification:

- Capture app status, gateway node status, and command output for each smoke step.
