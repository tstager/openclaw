# Windows Node Parity Implementation Plan

Source documents:

- `docs/plan/windows-node-feature-gap-list.md`
- `docs/plan/windows-node-shared-feature-comparison.md`

## Goal

Bring the OpenClaw Windows companion surface substantially in line with the external `openclaw-windows-node` implementation without replacing the current app's strongest patterns. This plan prioritizes missing foundational capabilities first, then deeper operational UX, then optional polish and repo-level follow-ons.

## What should be extended, not rewritten

The current `apps/windows` surface already has adequate parity in several core areas and should be **extended rather than replaced**:

- tray shell and single-instance lifetime,
- gateway lifecycle controls,
- native chat and session browsing,
- approvals and pairing queue UX,
- base Canvas/A2UI integration,
- Devices page, overlays, hotkeys, and recent theme customization.

The external repo is mostly ahead on **depth**, not on whether these categories exist at all.

## Priority buckets

### Foundational parity gaps

1. Expanded persisted settings and service seams
2. SSH tunnel management
3. `openclaw://` deep-link handling
4. Port diagnostics
5. Structured JSONL diagnostics
6. Activity stream and notification history
7. Persistent exec approval policy
8. URL risk evaluation
9. Browser proxy capability
10. Screen recording
11. Windows text-to-speech
12. Localization scaffold

### Later or optional parity work

1. ElevenLabs provider
2. Notification rule editor polish
3. Full onboarding wizard parity
4. PowerToys Command Palette extension
5. WinNode CLI utility
6. Full native A2UI renderer parity
7. Additional locale packs
8. Screen recording audio capture

## Sequencing principles

1. **Build shared seams before adding features.** Settings, service registration, diagnostics sinks, and topology handling should land before feature-specific UI.
2. **Prefer incremental extension of the current shell.** Do not rebuild the app into a clone of the external repo's window model.
3. **Land safety rails before high-risk capabilities.** Persistent approvals, URL risk evaluation, and redaction should precede browser proxy and richer media flows.
4. **Treat repo-level extras as follow-ons.** Command Palette and WinNode CLI parity matter, but they should not block the core `apps/windows` roadmap.

## Implementation sessions

## Session 1 - Settings and service scaffolding

**Goal:** create the app-level seams needed for parity work without making `MainWindow.cs` more monolithic.

**In scope**

- Expand the persisted settings model beyond the current appearance/basic shell preferences.
- Introduce service registration slots for future diagnostics, history, tunnel, browser proxy, and approval-policy services.
- Add only the navigation placeholders needed for later sessions.
- Extract small helper/service seams from `MainWindow.cs` where it reduces future coupling.

**Likely files**

- `apps/windows/OpenClaw.Windows/AppBootstrap.cs`
- `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs`
- `apps/windows/OpenClaw.Windows/App.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- `apps/windows/OpenClaw.Windows/WindowsNavigation.cs`
- `apps/windows/OpenClaw.Windows.Tests/AppPreferencesStoreTests.cs`

**Dependencies:** none

**Main risks**

- preference migration regressions,
- adding settings without clear ownership boundaries,
- making the main window harder to evolve.

**Recommended proof**

- settings round-trip and fallback tests,
- Windows companion launch/settings smoke,
- existing Windows test/build lane.

## Session 2 - Connection topology foundation

**Goal:** support non-direct gateway topologies and app-driven entry points.

**In scope**

- Add SSH tunnel settings and lifecycle management.
- Add unpackaged `openclaw://` registration and deep-link handling.
- Add second-instance handoff so deep links route into the running app.
- Add a port/topology model for gateway, tunnel, and future browser-proxy ports.
- Surface topology and tunnel state in the existing shell.

**Likely files**

- `apps/windows/OpenClaw.Windows/App.cs`
- `apps/windows/OpenClaw.Windows/AppBootstrap.cs`
- `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs`
- new `apps/windows/OpenClaw.Windows/SshTunnelService.cs`
- new `apps/windows/OpenClaw.Windows/DeepLinkHandler.cs`
- new `apps/windows/OpenClaw.Windows/PortDiagnosticsService.cs`

**Dependencies:** Session 1

**Main risks**

- unpackaged protocol-registration edge cases,
- `ssh.exe` availability and subprocess management,
- incorrect UI-thread marshaling from tray and deep-link callbacks.

**Recommended proof**

- direct/manual `openclaw://` route tests,
- start/stop/restart tunnel proof,
- unit tests for endpoint and port derivation.

## Session 3 - Structured diagnostics backbone

**Goal:** add the history and telemetry substrate that later UX surfaces can reuse.

**In scope**

- Structured JSONL diagnostics with bounded async queue and rotation.
- Activity stream service for app, gateway, device, and tunnel events.
- Notification history storage beyond the current latest-only state.
- Structured event emission from existing gateway and device actions.

**Likely files**

- `apps/windows/OpenClaw.Windows/App.cs`
- `apps/windows/OpenClaw.Windows/WindowsCompanionCoordinator.cs`
- `apps/windows/OpenClaw.Windows/LogsDiagnosticsSummary.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- new `apps/windows/OpenClaw.Windows/DiagnosticsJsonlService.cs`
- new `apps/windows/OpenClaw.Windows/ActivityStreamService.cs`
- new `apps/windows/OpenClaw.Windows/NotificationHistoryService.cs`

**Dependencies:** Session 1

**Main risks**

- shutdown/disposal ordering bugs,
- excessive log volume,
- leaking sensitive values into diagnostics.

**Recommended proof**

- queue/rotation tests,
- manual event-generation smoke with JSONL verification,
- logs surface verification from the current shell.

## Session 4 - History and diagnostics UX

**Goal:** expose the new diagnostics backends in usable Windows UI surfaces.

**In scope**

- Activity stream window or page with filtering and copy/clear support.
- Notification history window or page with counts, timestamps, deep links, and clear-all.
- Support-bundle or copy-summary actions.
- Tight integration with tray/deep-link entry points.

**Likely files**

- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- `apps/windows/OpenClaw.Windows/WindowsNavigation.cs`
- `apps/windows/OpenClaw.Windows/App.cs`
- new history/activity window files under `apps/windows/OpenClaw.Windows/`

**Dependencies:** Session 3

**Main risks**

- secondary-window lifetime management in WinUI 3,
- over-coupling new windows back into `MainWindow.cs`,
- accessibility/focus regressions.

**Recommended proof**

- manual tray/deep-link open flows,
- keyboard and focus smoke,
- docs-only `git diff --check` plus Windows test/build lane if code changes touch runtime paths.

## Session 5 - Security and policy foundation

**Goal:** add local safety rails before expanding browser and media capabilities.

**In scope**

- Persistent exec approval policy with allow/deny/prompt rules.
- Extended approvals UX with persistent decisions where appropriate.
- URL risk evaluation before browser/media navigation or fetch handoff.
- A2UI secret redaction for logged or surfaced data.
- Optional: persist notification categorization rules even if editor UX lands later.

**Likely files**

- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs`
- `apps/windows/OpenClaw.Windows/WindowsCanvasA2UI.cs`
- `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs` or a dedicated policy store
- new `apps/windows/OpenClaw.Windows/ExecApprovalPolicy.cs`
- new `apps/windows/OpenClaw.Windows/HttpUrlRiskEvaluator.cs`

**Dependencies:** Sessions 1 and 3

**Main risks**

- false-positive URL warnings,
- confusing one-off approvals vs stored policy,
- incomplete redaction coverage.

**Recommended proof**

- rule-precedence tests,
- URL risk classification tests,
- manual one-time vs persistent approval flows.

## Session 6 - Browser proxy capability

**Goal:** close one of the most important missing native-node features.

**In scope**

- Add a `browser.proxy` capability/service.
- Resolve direct and SSH-forwarded browser-control endpoints.
- Handle auth, timeout, path normalization, and file results.
- Surface browser proxy status and repair guidance in the shell.

**Likely files**

- `apps/windows/OpenClaw.Windows/AppBootstrap.cs`
- `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- topology services from Session 2
- new browser proxy service/capability files under `apps/windows/OpenClaw.Windows/`

**Dependencies:** Sessions 2 and 5

**Main risks**

- auth mismatch with browser-control hosts,
- bad assumptions about localhost-only control paths,
- confusing failure states when tunnel forwarding is incomplete.

**Recommended proof**

- manual direct-topology proof,
- manual SSH-forwarded proof,
- normalization and timeout tests.

## Session 7 - Media and voice parity

**Goal:** expand the existing Devices surface with higher-value external capabilities.

**In scope**

- MP4 screen recording with bounded duration/fps and screen selection.
- Integration into the current Devices UX and any node-capability plumbing required.
- Windows speech-synthesis/TTS provider.
- Leave audio capture and ElevenLabs as explicit follow-up work unless scope stays small.

**Likely files**

- `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs`
- new `apps/windows/OpenClaw.Windows.Native/ScreenRecordingService.cs`
- new `apps/windows/OpenClaw.Windows/TextToSpeechService.cs`

**Dependencies:** Sessions 1 and 5

**Main risks**

- `Windows.Graphics.Capture` and transcoding complexity,
- recording memory pressure,
- TTS playback/interruption edge cases.

**Recommended proof**

- manual MP4 capture proof,
- manual TTS playback and interrupt proof,
- settings persistence and argument-clamping tests.

## Session 8 - Onboarding depth and notification rules

**Goal:** improve guided setup and rule-backed notification handling after the foundations exist.

**In scope**

- Expand onboarding from diagnostics-only checks toward guided connection/setup flows.
- Add stored notification categorization rules and minimal management UI.
- Reuse topology, tunnel, and diagnostics services from earlier sessions rather than duplicating them.

**Likely files**

- `apps/windows/OpenClaw.Windows/OnboardingCheckService.cs`
- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs`
- new onboarding helper/state files under `apps/windows/OpenClaw.Windows/`

**Dependencies:** Sessions 1 through 5

**Main risks**

- overbuilding a wizard that does not fit the current shell,
- mixing onboarding state with general runtime state,
- notification-rule UX complexity.

**Recommended proof**

- onboarding route smoke,
- notification rule persistence tests,
- manual notification categorization proof.

## Session 9 - Localization and repo-level parity follow-ons

**Goal:** finish the highest-value remaining parity items once the core Windows companion is mature.

**In scope**

- Add MRT/resource-based localization scaffolding and localize the core shell.
- Add one non-English locale as the initial proof set.
- Evaluate repo-level follow-ons that are not core `apps/windows` work:
  - PowerToys Command Palette extension
  - WinNode CLI utility
  - additional locale packs
  - ElevenLabs provider
  - screen-recording audio capture

**Likely files**

- `apps/windows/OpenClaw.Windows/MainWindow.cs`
- `apps/windows/OpenClaw.Windows/App.cs`
- `apps/windows/OpenClaw.Windows/WindowsNavigation.cs`
- new `apps/windows/OpenClaw.Windows/Strings\*\Resources.resw`
- new repo-level project folders if Command Palette or WinNode CLI work is accepted

**Dependencies:** Sessions 1 through 8 for app-local parity; repo-level follow-ons can be split further

**Main risks**

- programmatic UI string extraction effort,
- layout regressions from longer translated strings,
- scope spill beyond `apps/windows`.

**Recommended proof**

- language-switch smoke across at least two locales,
- accessibility and keyboard smoke on updated surfaces,
- separate validation plans for Command Palette and CLI work if those sessions are taken on.

## Suggested cut lines

### "Substantial parity" milestone

Sessions 1, 2, 3, 5, 6, and 7 deliver the biggest functional gains and close the most important external gaps.

### "Operational UX parity" milestone

Add Session 4 and the notification/history portions of Session 8.

### "Broader product parity" milestone

Add Session 9 and any repo-level follow-ons that still provide clear user value.

## Immediate recommendation

Start with **Session 1**, then **Session 2**, then **Session 3**. That sequence creates the settings, topology, and diagnostics seams that the remaining sessions depend on. After that, prioritize **Session 5** before **Session 6** so browser-proxy and richer media work land on top of persistent policy and URL-safety infrastructure instead of bypassing it.
