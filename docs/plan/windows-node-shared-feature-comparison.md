# Windows Shared Feature Comparison

Compared repositories:

- **External reference repo:** `openclaw-windows-node\src`
- **Current repo surface:** `apps/windows`

This report covers the major feature areas that are implemented in **both** projects and were intentionally skipped from `docs/plan/windows-node-feature-gap-list.md`. The goal here is parity analysis: what each shared feature does today, how each repo implements it, and which side has the deeper or broader subfeature set.

## Summary

Both projects already implement the same core companion-app shape:

1. tray-driven Windows shell behavior,
2. gateway lifecycle and status surfaces,
3. onboarding/setup flows,
4. realtime gateway operator connectivity,
5. chat and session browsing,
6. approvals and pairing workflows,
7. Canvas/A2UI rendering,
8. logs/diagnostics,
9. Windows-native device tools,
10. appearance/settings customization.

The biggest implementation differences are consistent across the shared areas:

- The current `apps/windows` app tends to use a **simpler, companion-focused implementation** with direct WinUI/C# state models and narrower UX surfaces.
- The external repo tends to have **deeper operational UX and native tooling**, including richer onboarding, more dashboard instrumentation, stricter A2UI host infrastructure, more persistent rule/settings systems, and broader Windows capability coverage.
- In one notable area, the current repo is already ahead on **overlay-focused device UX**, while the external repo is clearly ahead on **screen recording, browser proxy, TTS, localization, and activity/history tooling**.

## Comparison table

| Shared feature area | Current repo implementation | External repo implementation | Main differences |
| --- | --- | --- | --- |
| **Tray shell + single instance** | WinUI app startup enforces one instance with a process mutex and uses a WinForms-based tray bridge for show/home/logs/settings/gateway actions. | Headless WinUI tray process uses a mutex plus deep-link forwarding, crash tombstones, and startup recovery markers. | External is more robust around crash recovery, deep-link handoff, and isolated test/runtime startup. |
| **Gateway lifecycle + dashboard** | Companion controller wraps CLI install/start/stop/restart/status and feeds a home/dashboard summary. | Command Center tracks richer channel/session/node/usage state, health timers, and SSH/tunnel actions. | Both manage gateway status, but external has a much deeper operational dashboard. |
| **Onboarding / setup** | Onboarding checks focus on CLI/node/gateway/pairing readiness and degrade to warning states when data is missing. | Multi-route onboarding wizard supports multiple connection modes, wizard RPC state, permissions, chat, and ready flows. | Current is a diagnostics summary; external is a full guided onboarding product. |
| **Realtime gateway connection** | WebSocket operator client supports connect challenge flow, scopes, pairing/auth states, RPC requests, sessions, approvals, and pairing queues. | Shared gateway client adds explicit V2/V3 auth negotiation, bootstrap handoff auth, usage/session/node state, and more feature flags. | Same core operator connection exists in both; external has broader protocol compatibility and richer state. |
| **Chat workspace** | Dedicated chat workspace state machine, selected-session chat loading, send-in-flight tracking, and shell integration. | WebChatWindow hosts the gateway SPA in WebView2 and adds quick-send dialog and more chat-window behaviors. | Current is native-shell oriented; external is more web-hosted and interaction-rich. |
| **Session browsing** | Session list drives navigation into Chat and stores a selected session key. | Session surfaces include previews and more session management actions around the list. | Same basic session browsing exists; external exposes more session-side tooling. |
| **Command approvals** | Pending approvals are listed with explicit allow-once or deny actions and summary counts in the shell. | Approval model includes persistent policy rules, shell constraints, danger-pattern filtering, and `AlwaysAllow`. | Current is queue-driven; external adds persistent policy and broader execution controls. |
| **Pairing workflows** | Unified UI list for device and node pairing with approve/reject routing per pairing kind. | Pairing is integrated with onboarding, setup-code decoding, and deeper bootstrap flows. | Same pairing concept exists; external ties it into a richer setup model. |
| **Canvas / A2UI** | Windows canvas commands, trusted-host resolution, renderer-result parsing, and strict A2UI v0.8 JSONL validation. | Full A2UI router/renderer/theme host with per-surface state, action routing, telemetry, caps, and secret redaction. | Both support Canvas/A2UI, but external is a fuller native host implementation. |
| **Logs / diagnostics** | Crash log, gateway log summary, copy/reveal/open-folder actions, and basic status/error reporting. | Structured JSONL diagnostics, activity stream, notification history, support-bundle export, and port ownership diagnostics. | Current covers the basics; external has much richer diagnostic depth and historical tooling. |
| **Windows device tools** | Screen snapshots, frame sequences, camera photo capture, device enumeration, overlays, toasts, and global hotkeys. | Adds screen recording, browser proxy, TTS, location, richer notification plumbing, and broader capability registration. | Current has a solid device page and overlay UX; external has a much broader node-capability stack. |
| **Appearance / settings** | Persists theme, accent, and color-theme preferences with a value-based palette resolver and settings UI. | Flat JSON settings model spans app/network/node/notification/theme/localization concerns plus localized resource strings. | Current goes deeper on handcrafted color-theme palettes; external goes much broader on settings scope and localization. |

## Detailed comparisons

### 1. Tray shell behavior and single-instance lifetime

Both apps are tray-oriented Windows companions that enforce one running instance and keep most user interaction anchored to tray-driven actions.

- **Current repo:** `App` owns the single-instance mutex, creates the service graph only after the instance check, wires tray callbacks back onto the WinUI dispatcher, and maintains an app-local crash log. `WindowsTrayHost` provides the tray menu/status bridge used by the shell.  
  Current evidence: `apps/windows/OpenClaw.Windows/App.cs:8-29`, `apps/windows/OpenClaw.Windows/App.cs:30-86`, `apps/windows/OpenClaw.Windows/App.cs:106-169`, `apps/windows/OpenClaw.Windows.Native/WindowsTrayHost.cs:6-27`, `apps/windows/OpenClaw.Windows.Native/WindowsTrayHost.cs:29-84`
- **External repo:** `App.xaml.cs` uses a named mutex, secondary-launch deep-link forwarding over a named pipe, a hidden keep-alive window, crash handling across multiple exception paths, and a `run.marker` tombstone file. It also includes autostart registry management.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:29-139`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:167-218`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:221-254`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:284-311`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\AutoStartManager.cs:1-51`

**Difference:** both have the same foundational tray/single-instance behavior, but the external repo has a more production-hardened lifetime model with crash tombstones and deep-link handoff.

### 2. Gateway lifecycle controls and dashboard status

Both projects let the Windows app manage the local gateway and reflect its health back into a shell/dashboard surface.

- **Current repo:** `GatewayCompanionController` drives CLI install/start/stop/restart/status flows, normalizes CLI output into a stable status snapshot, and feeds `WindowsCompanionCoordinator` / `GatewayDashboardSummary` for the home surface.  
  Current evidence: `apps/windows/OpenClaw.Windows/GatewayCompanionController.cs:5-18`, `apps/windows/OpenClaw.Windows/GatewayCompanionController.cs:26-75`, `apps/windows/OpenClaw.Windows/GatewayCompanionController.cs:77-134`, `apps/windows/OpenClaw.Windows/GatewayCompanionController.cs:145-228`, `apps/windows/OpenClaw.Windows/WindowsCompanionCoordinator.cs:24-42`, `apps/windows/OpenClaw.Windows/WindowsCompanionCoordinator.cs:54-81`, `apps/windows/OpenClaw.Windows/GatewayDashboardSummary.cs:4-40`, `apps/windows/OpenClaw.Windows/GatewayDashboardSummary.cs:43-79`
- **External repo:** the Command Center tracks connection status, channel health, sessions, nodes, usage/cost, and SSH tunnel state, with timer-driven health refresh and command events for channel toggles, dashboard navigation, update checks, and tunnel restart.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Windows\StatusDetailWindow.xaml.cs:1-70`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:74-89`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:360-394`

**Difference:** the current repo covers gateway lifecycle fundamentals; the external repo exposes a materially richer operations dashboard.

### 3. Onboarding checks and setup status

Both projects include startup/setup guidance, but they solve different depths of the onboarding problem.

- **Current repo:** `OnboardingCheckService` runs a lightweight diagnostic pass over OpenClaw CLI, Node, gateway reachability, and pairing readiness, then returns pass/warn/fail rows for the shell.  
  Current evidence: `apps/windows/OpenClaw.Windows/OnboardingCheckService.cs:22-79`, `apps/windows/OpenClaw.Windows/OnboardingCheckService.cs:81-138`, `apps/windows/OpenClaw.Windows/WindowsCompanionCoordinator.cs:44-52`
- **External repo:** `OnboardingState` defines a multi-route wizard with `Welcome`, `Connection`, `Wizard`, `Permissions`, `Chat`, and `Ready` pages, connection-mode branching, wizard RPC lifecycle state, and optional node-mode shortcuts.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Onboarding\Services\OnboardingState.cs:11-139`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:347-352`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Onboarding\Pages\`

**Difference:** both address setup readiness, but the current repo is a compact diagnostics surface while the external repo is a full onboarding wizard.

### 4. Realtime gateway/operator connection

Both apps have a realtime operator client that speaks to the gateway over WebSockets and handles auth/pairing/session concerns.

- **Current repo:** `GatewayRealtimeClient` defines connection states, requested operator scopes, connect-challenge handling, RPC request tracking, session listing, chat calls, approvals, pairing flows, and disconnect/error propagation.  
  Current evidence: `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:11-52`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:99-195`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:203-257`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:413-460`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:469-553`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:598-756`
- **External repo:** `OpenClawGatewayClient` tracks operator identity, bootstrap scopes, V2/V3 signature token modes, pairing/auth failure states, pending request maps, sessions, usage, notification rules, and wizard responses on top of a shared WebSocket base.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Shared\OpenClawGatewayClient.cs:12-79`, `openclaw-windows-node\src\OpenClaw.Shared\OpenClawGatewayClient.cs:81-140`, `openclaw-windows-node\src\OpenClaw.Shared\WebSocketClientBase.cs`

**Difference:** the shared core capability is present in both, but the external client supports more auth compatibility modes and more attached gateway state.

### 5. Chat workspace

Both apps provide an operator chat experience tied to gateway sessions.

- **Current repo:** `ChatWorkspaceState` keeps transcript and transport state separate, while `MainWindow` and `GatewayRealtimeClient` coordinate session-based chat loading and sending inside the native shell.  
  Current evidence: `apps/windows/OpenClaw.Windows/ChatWorkspaceState.cs:3-101`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:259-287`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2277-2332`
- **External repo:** `WebChatWindow` hosts the gateway web chat in WebView2, bridges native/web messages, and pairs that with a topmost `QuickSendDialog` and per-session activity tracking.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Windows\WebChatWindow.xaml.cs:1-58`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Dialogs\QuickSendDialog.cs:1-50`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:106-109`

**Difference:** current chat is more native-shell state driven; external chat is more web-hosted and supports quicker overlay-style interactions.

### 6. Session browsing

Both projects expose gateway sessions as a first-class browsing surface.

- **Current repo:** sessions come from `sessions.list`, normalize into `SessionSummary`, store the selected chat session key, and route the shell from Sessions into Chat.  
  Current evidence: `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:59-70`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:289-299`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:345-365`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2294-2308`, `apps/windows/OpenClaw.Windows/WindowsNavigation.cs:6-17`, `apps/windows/OpenClaw.Windows/WindowsNavigation.cs:30-40`
- **External repo:** the gateway client and tray app also track sessions, previews, and related session metadata for the Command Center and chat experience.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Shared\OpenClawGatewayClient.cs:45-57`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:77-85`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:106-109`

**Difference:** both have session browsing, but the external repo includes more preview/activity plumbing around the same core session list.

### 7. Command approvals

Both apps expose operator approval workflows for guarded actions.

- **Current repo:** pending approvals are surfaced as explicit UI rows with command/cwd/agent/session metadata, and the shell supports one-time allow vs deny plus approval summaries.  
  Current evidence: `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:72-80`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:301-307`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:327-343`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:367-376`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2814-2911`, `apps/windows/OpenClaw.Windows/OperatorWorkflowSummary.cs:3-49`
- **External repo:** `ExecApprovalPolicy` persists JSON rule sets, `ExecApprovalPrompt` supports `Deny`, `AllowOnce`, and `AlwaysAllow`, and the system capability adds dangerous-pattern checks and policy-backed `system.run` execution controls.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Shared\ExecApprovalPolicy.cs:1-60`, `openclaw-windows-node\src\OpenClaw.Shared\ExecApprovalPrompt.cs:1-40`, `openclaw-windows-node\src\OpenClaw.Shared\Capabilities\SystemCapability.cs:1-60`

**Difference:** both implement approvals, but the external repo adds persistent rules, shell scoping, and stronger local policy enforcement.

### 8. Pairing workflows

Both apps implement device/operator pairing, but they surface it differently.

- **Current repo:** device and node pairing queues are merged into one UI list, with approve/reject mapped to the right RPC and pairing state reflected into shell summaries.  
  Current evidence: `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:82-89`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:379-410`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2913-2955`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3000-3009`, `apps/windows/OpenClaw.Windows/OperatorWorkflowSummary.cs:23-49`
- **External repo:** pairing is wired into onboarding, setup-code decoding, and operator pairing scope handling.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Shared\OpenClawGatewayClient.cs:20-27`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Onboarding\Services\SetupCodeDecoder.cs`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Onboarding\Services\LocalGatewayApprover.cs`

**Difference:** both support the same broad pairing function; the external repo integrates pairing more deeply into guided setup flows.

### 9. Canvas / A2UI rendering

Both repos implement a Windows Canvas/A2UI surface, but at very different depths.

- **Current repo:** `WindowsCanvasA2UI` defines command names, trusted host resolution, renderer-result parsing, and strict validation for A2UI v0.8 JSONL messages.  
  Current evidence: `apps/windows/OpenClaw.Windows/WindowsCanvasA2UI.cs:5-30`, `apps/windows/OpenClaw.Windows/WindowsCanvasA2UI.cs:67-135`, `apps/windows/OpenClaw.Windows/WindowsCanvasA2UI.cs:145-185`, `apps/windows/OpenClaw.Windows/WindowsCanvasA2UI.cs:187-261`
- **External repo:** `A2UIRouter` hosts per-surface rendering on the UI thread, caps concurrent surfaces, tracks lifecycle events, and works with a fuller renderer/theme/action/telemetry stack.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Tray.WinUI\A2UI\Hosting\A2UIRouter.cs:14-117`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\A2UI\Rendering\Renderers\ContainerRenderers.cs`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\A2UI\Rendering\Renderers\DisplayRenderers.cs:27-60`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\A2UI\Rendering\Renderers\InteractiveRenderers.cs:11-52`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\A2UI\Theming\A2UITheme.cs:1-99`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\A2UI\Actions\GatewayActionTransport.cs`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\A2UI\Rendering\SecretRedactor.cs`

**Difference:** current repo has the protocol-validation and host-url seams needed for Canvas/A2UI support; external repo has the more complete native rendering engine.

### 10. Logs and diagnostics

Both repos provide user-visible diagnostics and logging surfaces.

- **Current repo:** `App` writes crash logs, `LogsDiagnosticsSummary` normalizes app/gateway log locations and errors, and `MainWindow` exposes copy/reveal/open-folder UI around those paths.  
  Current evidence: `apps/windows/OpenClaw.Windows/App.cs:129-169`, `apps/windows/OpenClaw.Windows/LogsDiagnosticsSummary.cs:5-55`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2335-2448`
- **External repo:** diagnostics are split between structured JSONL event logging, an activity stream with category/filter support, notification history, and TCP port owner diagnostics.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\DiagnosticsJsonlService.cs:1-139`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\ActivityStreamService.cs:1-80`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Windows\ActivityStreamWindow.xaml.cs:1-58`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\PortDiagnosticsService.cs:1-236`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Windows\NotificationHistoryWindow.xaml.cs`

**Difference:** both have diagnostics, but the current repo emphasizes immediate logs/status while the external repo adds historical, structured, and support-oriented tooling.

### 11. Windows-native device and integration tools

Both apps expose Windows integration capabilities, especially around capture, notifications, and hotkeys.

- **Current repo:** `WindowsDeviceCapabilityService` supports camera/microphone enumeration, primary-screen capture, frame-sequence capture, camera photos, permission summaries, and capture-file output; the broader Windows UI also exposes overlays, tray notifications, and global hotkeys.  
  Current evidence: `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs:11-24`, `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs:27-53`, `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs:55-166`, `apps/windows/OpenClaw.Windows/DeviceCapabilityPresentation.cs:5-58`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3095-3394`, `apps/windows/OpenClaw.Windows.Native/WindowsGlobalHotkeyService.cs:6-122`
- **External repo:** the tray app adds screen recording, richer screen capture options, camera capture via `MediaCapture`, message-window hotkeys, notification history/categorization, and broader capability registration including browser proxy, location, and TTS.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\ScreenCaptureService.cs:1-325`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\ScreenRecordingService.cs:1-50`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\CameraCaptureService.cs:1-80`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\GlobalHotkeyService.cs:1-339`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Services\NotificationHistoryService.cs:1-60`, `openclaw-windows-node\src\OpenClaw.Shared\Capabilities\`

**Difference:** both have real Windows device tooling; the current repo is better surfaced as a companion device page, while the external repo has the broader native/node capability stack.

### 12. Appearance, theming, and settings

Both apps support user customization, but they emphasize different scopes.

- **Current repo:** the Windows companion persists theme, accent color, and color theme, then resolves those preferences through `WindowsThemePaletteResolver` and the settings appearance UI.  
  Current evidence: `apps/windows/OpenClaw.Windows/WindowsThemePaletteResolver.cs:6-40`, `apps/windows/OpenClaw.Windows/WindowsThemePaletteResolver.cs:42-81`, `apps/windows/OpenClaw.Windows/WindowsThemePaletteResolver.cs:83-189`, `apps/windows/OpenClaw.Windows/WindowsThemePaletteResolver.cs:217-258`, `apps/windows/OpenClaw.Windows/MainWindow.cs:301-347`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3035-3092`, `apps/windows/OpenClaw.Windows/App.cs:43-49`
- **External repo:** `SettingsData` stores a wider cross-section of gateway, SSH, node-capability, notification, localization, and update preferences, while the tray app also supports locale overrides and MRT-backed resource localization.  
  External evidence: `openclaw-windows-node\src\OpenClaw.Shared\SettingsData.cs:1-83`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Windows\SettingsWindow.xaml.cs:1-60`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\App.xaml.cs:144-153`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Helpers\LocalizationHelper.cs:6-49`, `openclaw-windows-node\src\OpenClaw.Tray.WinUI\Strings\`

**Difference:** both have appearance/settings functionality; the current repo is deeper on custom palette behavior, while the external repo is much broader on settings scope and localization infrastructure.

## Takeaways

1. **Core companion parity already exists.** The current repo is not missing the basic tray/gateway/chat/session/approval/pairing/canvas/logs/theme shell.
2. **The external repo is mostly ahead in depth, not category count.** Most overlap areas exist in both projects, but the external implementation usually has more supporting subfeatures, persistence, and operational UX.
3. **The biggest parity deltas within shared features are onboarding depth, approval policy persistence, A2UI host sophistication, and diagnostics history.**
4. **The current repo already has a viable Windows companion foundation** and is strongest where it presents a compact native shell with a dedicated Devices surface and newer custom theme work.
