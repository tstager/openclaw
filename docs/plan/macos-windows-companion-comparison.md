# macOS vs Windows Companion Comparison

Compared repositories:

- **macOS app:** `apps/macos`
- **Windows app:** `apps/windows`

This document compares the current OpenClaw macOS app against the Windows companion app, focusing on **feature coverage** and **implementation approach**. Unlike the earlier Windows-vs-external reports, this comparison is between two app surfaces in the same repo.

## Summary

The two apps overlap on the core companion shape—tray/menu bar shell behavior, gateway control, realtime connectivity, chat, sessions, approvals, pairing, Canvas/A2UI, diagnostics, notifications, and settings—but they are not equivalent in scope.

- **`apps/macos` is the broader product surface.** It behaves like a full macOS control center and capability host: launchd-managed local gateway lifecycle, remote SSH/direct/Tailscale modes, a richer onboarding flow, node-mode capability hosting, voice wake/talk features, a large settings surface, and the Peekaboo automation bridge. Evidence: `apps/macos/Sources/OpenClaw/MenuBar.swift:10-160`, `apps/macos/Sources/OpenClaw/GatewayProcessManager.swift:1-447`, `apps/macos/Sources/OpenClaw/Onboarding.swift:1-183`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeModeCoordinator.swift:5-180`, `apps/macos/Sources/OpenClaw/VoiceWakeRuntime.swift:10-140`, `apps/macos/Sources/OpenClaw/PeekabooBridgeHostCoordinator.swift:1-193`, `apps/macos/Sources/OpenClaw/SettingsRootView.swift:1-332`
- **`apps/windows` is a narrower companion shell with strong Windows-native integrations.** Its strengths are a compact code-built WinUI shell, tray-first gateway controls, a dedicated native Devices surface, overlays/hotkeys, and recent custom theme work. Evidence: `apps/windows/OpenClaw.Windows/App.cs:7-127`, `apps/windows/OpenClaw.Windows/GatewayCompanionController.cs:16-321`, `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs:11-166`, `apps/windows/OpenClaw.Windows.Native/WindowsGlobalHotkeyService.cs:6-122`, `apps/windows/OpenClaw.Windows/WindowsThemePaletteResolver.cs:22-258`
- **The largest product differences are not cosmetic.** macOS owns several major capabilities that Windows does not yet have: node-mode breadth, richer topology/discovery support, persistent exec-approval policy depth, voice wake/talk mode, and the Peekaboo bridge. Windows, however, has a more explicit user-facing native device test surface and more bespoke appearance customization. Evidence: `apps/macos/Sources/OpenClaw/NodeMode/MacNodeModeCoordinator.swift:70-180`, `apps/macos/Sources/OpenClaw/RemoteTunnelManager.swift:1-138`, `apps/macos/Sources/OpenClaw/ExecApprovals.swift`, `apps/macos/Sources/OpenClaw/VoiceWakeRuntime.swift:10-140`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3095-3405`, `apps/windows/OpenClaw.Windows/WindowsThemePaletteResolver.cs:22-258`

## Comparison table

| Feature area | macOS app | Windows app | Main difference |
| --- | --- | --- | --- |
| **Shell model** | Menu bar app with animated critter status, hover HUD, Dock toggle, richer panel routing | Single-window tray-first WinUI shell with tray commands | macOS is more specialized and ambient; Windows is more straightforward and app-window oriented |
| **Gateway lifecycle** | launchd attach-or-bootstrap manager with local/remote/unconfigured modes | CLI-driven install/start/stop/restart/status controller | macOS has deeper topology/process management |
| **Onboarding** | Multi-page wizard with mode-aware routing and onboarding chat | Diagnostics-oriented setup checks | macOS onboarding is much broader |
| **Realtime connection** | Persistent push/control channel with recovery and richer coordination across modes | WebSocket operator client with scope/auth/pairing handling | Both are substantial; Windows is simpler but focused |
| **Chat / sessions** | Dashboard/web-style interaction model plus broader surrounding shell | Native chat transcript and session switcher | Windows is simpler and more native-shell centric |
| **Approvals / pairing** | Deep exec approval model with policy/allowlist and separate pairing prompters | Queue-based approvals and merged device/node pairing lists | macOS has deeper policy; Windows has a more compact operator dashboard UX |
| **Canvas / A2UI** | WKWebView-based canvas runtime with local file serving and action bridge | WebView2-backed node client and A2UI bridge | macOS implementation is deeper and more complete |
| **Diagnostics / history** | More distributed operational state and richer settings/admin surfaces | Simpler logs + event visibility + tray notification history | Windows exposes diagnostics more directly inside one shell; macOS spreads them across more subsystems |
| **Native integrations** | Node-mode capabilities, remote tunnels, Tailscale, voice, automation bridge | Device capture tools, overlays, global hotkeys, WinUI theming | macOS is broader overall; Windows is stronger in companion-style device tooling |
| **Settings** | Large multi-tab admin/settings center | Focused companion settings plus custom theme palette | macOS is much broader administratively; Windows is narrower but more opinionated visually |

## Detailed comparison

### 1. Shell architecture and app lifetime

Both apps live primarily in the OS chrome rather than as conventional document windows, but they choose very different shell models.

- **macOS:** `OpenClawApp` is a `MenuBarExtra`-based app with a custom `CritterStatusLabel`, hover HUD suppression logic, explicit left-click/right-click routing, Dock visibility management, and app delegate startup sequencing. The menu bar item is a first-class interaction surface, not just a status icon. Evidence: `apps/macos/Sources/OpenClaw/MenuBar.swift:10-160`, `apps/macos/Sources/OpenClaw/MenuBar.swift:145-160`, `apps/macos/Sources/OpenClaw/DockIconManager.swift`, `apps/macos/Sources/OpenClaw/CritterStatusLabel.swift`
- **Windows:** `App` enforces a single-instance mutex, creates `MainWindow`, applies persisted appearance state, and uses `WindowsTrayHost` as a bridge for show/home/logs/settings/gateway actions. The tray host is important, but the primary UI model is still a single companion window. Evidence: `apps/windows/OpenClaw.Windows/App.cs:7-127`, `apps/windows/OpenClaw.Windows.Native/WindowsTrayHost.cs:6-84`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2117-2136`

**Difference:** macOS feels like a menu bar product with richer ambient presence, while Windows feels like a tray-launched companion control panel.

### 2. Gateway lifecycle and connection topology

Both apps manage the gateway, but macOS covers more deployment topologies and process states.

- **macOS:** `GatewayProcessManager` can attach to an existing gateway before spawning one, manage a launchd service, respect attach-only mode, and coordinate local vs remote vs unconfigured connection modes. The app also has SSH tunnel management, gateway discovery, and Tailscale-aware discovery/setup paths. Evidence: `apps/macos/Sources/OpenClaw/GatewayProcessManager.swift:1-447`, `apps/macos/Sources/OpenClaw/GatewayLaunchAgentManager.swift`, `apps/macos/Sources/OpenClaw/RemoteTunnelManager.swift:1-138`, `apps/macos/Sources/OpenClaw/ConnectionModeCoordinator.swift`, `apps/macos/Sources/OpenClaw/TailscaleService.swift:1-182`, `apps/macos/Sources/OpenClawDiscovery/`
- **Windows:** `GatewayCompanionController` wraps CLI-driven install/start/stop/restart/status flows and normalizes gateway status into a UI-friendly snapshot, but it currently assumes a much simpler local companion topology. Evidence: `apps/windows/OpenClaw.Windows/GatewayCompanionController.cs:16-321`, `apps/windows/OpenClaw.Windows/WindowsCompanionCoordinator.cs:24-52`, `apps/windows/OpenClaw.Windows/GatewayDashboardSummary.cs:3-85`

**Difference:** macOS is a richer gateway/topology orchestrator; Windows is a more local-service-oriented companion controller.

### 3. Onboarding and first-run setup

This is one of the clearest scope gaps.

- **macOS:** the onboarding stack spans multiple routed pages, mode-specific page ordering, CLI install prompting, permissions setup, gateway discovery, wizard/setup steps, and onboarding chat. Evidence: `apps/macos/Sources/OpenClaw/Onboarding.swift:1-183`, `apps/macos/Sources/OpenClaw/OnboardingView+Pages.swift`, `apps/macos/Sources/OpenClaw/OnboardingView+Wizard.swift`, `apps/macos/Sources/OpenClaw/OnboardingView+Chat.swift`, `apps/macos/Sources/OpenClaw/CLIInstaller.swift`, `apps/macos/Sources/OpenClaw/CLIInstallPrompter.swift`
- **Windows:** `OnboardingCheckService` is closer to a readiness diagnostic layer for CLI, Node, gateway status, and pairing, surfaced through the shell rather than a full guided wizard. Evidence: `apps/windows/OpenClaw.Windows/OnboardingCheckService.cs:22-79`, `apps/windows/OpenClaw.Windows/OnboardingCheckService.cs:81-138`, `apps/windows/OpenClaw.Windows/WindowsCompanionCoordinator.cs:44-52`

**Difference:** macOS has a true onboarding product; Windows currently has setup diagnostics.

### 4. Realtime gateway control path

Both apps have substantial realtime logic, but they optimize for different surrounding architectures.

- **macOS:** `ControlChannel` and related coordination layers drive push subscription, work activity, event logging, and reconnection across local and remote modes. Evidence: `apps/macos/Sources/OpenClaw/ControlChannel.swift:1-449`, `apps/macos/Sources/OpenClaw/GatewayPushSubscription.swift`, `apps/macos/Sources/OpenClaw/GatewayConnectivityCoordinator.swift:1-63`
- **Windows:** `GatewayRealtimeClient` is an explicit operator WebSocket client that handles connect challenges, requested scopes, device identity, auth failure, pairing-required states, and RPC helpers for chat/sessions/approvals/pairing. Evidence: `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:12-31`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:99-206`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:413-572`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:598-699`

**Difference:** both are capable, but macOS ties realtime control into a broader multi-mode app runtime, while Windows has a more directly operator-focused companion client.

### 5. Chat and session handling

Both apps expose chat/session workflows, but Windows keeps them more plainly surfaced in the native shell.

- **macOS:** chat is part of a broader dashboard/web-panel style environment, including onboarding chat and deeper surrounding shell coordination. Evidence: `apps/macos/Sources/OpenClaw/OnboardingView+Chat.swift`, `apps/macos/Sources/OpenClaw/MenuBar.swift:145-160`
- **Windows:** `ChatWorkspaceState`, `MainWindow`, and `GatewayRealtimeClient` implement a native transcript view, send flow, and session browser/switcher without much extra ornamentation. Evidence: `apps/windows/OpenClaw.Windows/ChatWorkspaceState.cs:3-101`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:259-299`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2241-2333`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2716-2801`

**Difference:** Windows is more explicit about chat/session as native companion surfaces; macOS integrates them into a broader product shell.

### 6. Approvals, policy, and pairing

Both apps support approvals and pairing, but macOS goes much further into persistent local policy.

- **macOS:** `ExecApprovals.swift` implements a multi-level security model (`deny`, `allowlist`, `full`), persistent pattern matching, interactive allow-once/allow-always prompts, and a Unix socket server for approval requests. Pairing also has dedicated device/node approval prompters. Evidence: `apps/macos/Sources/OpenClaw/ExecApprovals.swift`, `apps/macos/Sources/OpenClaw/ExecApprovalsSocket.swift`, `apps/macos/Sources/OpenClaw/ExecApprovalsGatewayPrompter.swift`, `apps/macos/Sources/OpenClaw/DevicePairingApprovalPrompter.swift`, `apps/macos/Sources/OpenClaw/NodePairingApprovalPrompter.swift`
- **Windows:** approvals are queue-backed and user-facing, with explicit allow-once/deny actions; pairing merges device and node requests into one operator workflow surface. Evidence: `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:302-307`, `apps/windows/OpenClaw.Windows/GatewayRealtimeClient.cs:367-410`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2814-3010`, `apps/windows/OpenClaw.Windows/OperatorWorkflowSummary.cs:3-49`

**Difference:** Windows has a cleaner, simpler operator review flow; macOS has the deeper local-policy engine.

### 7. Canvas and A2UI

Both apps implement Canvas/A2UI, but the macOS version is much closer to a full embedded runtime.

- **macOS:** `CanvasManager` and `CanvasWindowController` host a `WKWebView`, serve local canvas files through a custom `canvas://` scheme, auto-navigate to the gateway's A2UI surface URL, inject an action bridge, and support eval/snapshot/fullscreen/file watching. Evidence: `apps/macos/Sources/OpenClaw/CanvasManager.swift:1-346`, `apps/macos/Sources/OpenClaw/CanvasWindowController.swift`, `apps/macos/Sources/OpenClaw/CanvasWindowController+Navigation.swift`, `apps/macos/Sources/OpenClaw/CanvasA2UIActionMessageHandler.swift:1-145`, `apps/macos/Sources/OpenClaw/CanvasSchemeHandler.swift`, `apps/macos/Sources/OpenClaw/CanvasFileWatcher.swift`
- **Windows:** `WindowsCanvasNodeClient` connects as a node client, advertises Canvas commands, refreshes the plugin surface URL, derives a trusted A2UI host URL, and uses WebView2 plus `WindowsCanvasA2UI` parsing/validation for A2UI interaction. `canvas.snapshot` remains unimplemented. Evidence: `apps/windows/OpenClaw.Windows/WindowsCanvasNodeClient.cs:20-119`, `apps/windows/OpenClaw.Windows/WindowsCanvasNodeClient.cs:170-180`, `apps/windows/OpenClaw.Windows/WindowsCanvasNodeClient.cs:315-450`, `apps/windows/OpenClaw.Windows/WindowsCanvasA2UI.cs:5-40`, `apps/windows/OpenClaw.Windows/WindowsCanvasA2UI.cs:145-261`, `apps/windows/OpenClaw.Windows/MainWindow.cs:1589-2052`

**Difference:** both have meaningful Canvas support, but macOS is broader and more complete; Windows is still a partial node-driven Canvas host.

### 8. Native capability hosting

This is the biggest functional divergence.

- **macOS:** `MacNodeModeCoordinator` and `MacNodeRuntime` make the app a real capability node, advertising caps and commands for canvas, screen, browser, camera, and location, with a large command surface and runtime dispatch model. Evidence: `apps/macos/Sources/OpenClaw/NodeMode/MacNodeModeCoordinator.swift:5-180`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeRuntime.swift`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeScreenCommands.swift`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeBrowserProxy.swift`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeLocationService.swift`
- **Windows:** the app does have a Canvas node path plus a rich local Devices surface, but it does not yet expose a comparably broad general-purpose node-capability host. Its strengths are local capture tools, notifications, overlays, and hotkeys. Evidence: `apps/windows/OpenClaw.Windows/WindowsCanvasNodeClient.cs:20-119`, `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs:11-166`, `apps/windows/OpenClaw.Windows.Native/WindowsGlobalHotkeyService.cs:6-122`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3095-3405`

**Difference:** macOS is a true capability host; Windows is still mostly a companion shell plus a narrower native tool surface.

### 9. Voice, audio, and automation

This is where macOS has entire feature families that Windows does not currently match.

- **macOS:** `VoiceWakeRuntime`, `TalkModeRuntime`, `TalkModeController`, and related overlay/push-to-talk pieces implement always-on trigger listening, talk mode, TTS playback, and push-to-talk. The app also ships the Peekaboo automation bridge for UI automation and capture over a local socket. Evidence: `apps/macos/Sources/OpenClaw/VoiceWakeRuntime.swift:10-140`, `apps/macos/Sources/OpenClaw/TalkModeController.swift:1-107`, `apps/macos/Sources/OpenClaw/TalkModeRuntime.swift`, `apps/macos/Sources/OpenClaw/VoicePushToTalk.swift`, `apps/macos/Sources/OpenClaw/PeekabooBridgeHostCoordinator.swift:1-193`
- **Windows:** current native integrations are centered on device capture, overlays, notifications, and hotkeys rather than full voice/automation systems. Evidence: `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs:39-166`, `apps/windows/OpenClaw.Windows.Native/WindowsGlobalHotkeyService.cs:6-122`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3095-3405`

**Difference:** voice wake/talk mode and Peekaboo are major macOS-only features.

### 10. Notifications, diagnostics, and operational state

Both apps surface health and activity, but in different shapes.

- **macOS:** health, heartbeat, presence, and notifications are spread across dedicated managers and stores, with the wider operational story living inside the menu bar runtime, remote mode, and settings system. Evidence: `apps/macos/Sources/OpenClaw/NotificationManager.swift:1-66`, `apps/macos/Sources/OpenClaw/PresenceReporter.swift`, `apps/macos/Sources/OpenClaw/HealthStore.swift`, `apps/macos/Sources/OpenClaw/HeartbeatStore.swift`
- **Windows:** logs/diagnostics are concentrated into one companion shell: crash log, gateway log summary, copy/reveal actions, raw log preview, session event visibility, tray balloon notifications, and recent in-memory activity. Evidence: `apps/windows/OpenClaw.Windows/App.cs:129-169`, `apps/windows/OpenClaw.Windows/LogsDiagnosticsSummary.cs:5-55`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2335-2672`, `apps/windows/OpenClaw.Windows/WindowsNotificationActivity.cs:19-75`, `apps/windows/OpenClaw.Windows.Native/WindowsTrayHost.cs:70-76`

**Difference:** Windows is more centralized and obvious in its diagnostic UI; macOS has more operational subsystems but fewer equally centralized companion-style surfaces.

### 11. Settings and configuration surface

The settings difference is mostly about breadth.

- **macOS:** `SettingsRootView` is a wide admin/configuration center with many tabs covering connection, permissions, voice/talk, channels, skills, cron, exec approvals, sessions, instances, config editing, debug, and about/update flows. Evidence: `apps/macos/Sources/OpenClaw/SettingsRootView.swift:1-332`, `apps/macos/Sources/OpenClaw/GeneralSettings.swift`, `apps/macos/Sources/OpenClaw/PermissionsSettings.swift`, `apps/macos/Sources/OpenClaw/VoiceWakeSettings.swift`, `apps/macos/Sources/OpenClaw/ChannelsSettings.swift`, `apps/macos/Sources/OpenClaw/CronSettings.swift`, `apps/macos/Sources/OpenClaw/ExecApprovalsSettings.swift`, `apps/macos/Sources/OpenClaw/ConfigSettings.swift`
- **Windows:** settings are more focused on companion concerns such as gateway endpoint/token, startup behavior, notification categories, Canvas enablement, voice controls, global hotkeys, and appearance. The implementation is heavily code-built rather than XAML-page-driven. Evidence: `apps/windows/OpenClaw.Windows/MainWindow.cs:961-1040`, `apps/windows/OpenClaw.Windows/MainWindow.cs:1385-1435`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3035-3092`, `apps/windows/OpenClaw.Windows/AppPreferencesStore.cs:42-235`

**Difference:** macOS is much broader administratively; Windows is narrower but more cohesive as a companion app.

### 12. Platform-specific strengths

Each app has areas where it is not merely different, but clearly optimized for its platform.

- **macOS strengths:** menu bar-native shell design, launchd lifecycle, remote/discovery depth, capability-node breadth, voice wake/talk features, Peekaboo automation bridge, larger settings/admin surface. Evidence: `apps/macos/Sources/OpenClaw/MenuBar.swift:10-160`, `apps/macos/Sources/OpenClaw/GatewayProcessManager.swift:1-447`, `apps/macos/Sources/OpenClaw/RemoteTunnelManager.swift:1-138`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeModeCoordinator.swift:70-180`, `apps/macos/Sources/OpenClaw/VoiceWakeRuntime.swift:10-140`, `apps/macos/Sources/OpenClaw/PeekabooBridgeHostCoordinator.swift:1-193`, `apps/macos/Sources/OpenClaw/SettingsRootView.swift:1-332`
- **Windows strengths:** dedicated native device tooling page, overlays, hotkeys, compact operator dashboard, direct native chat/session shell, and stronger custom appearance work. Evidence: `apps/windows/OpenClaw.Windows.Native/WindowsDeviceCapabilityService.cs:11-166`, `apps/windows/OpenClaw.Windows.Native/WindowsGlobalHotkeyService.cs:6-122`, `apps/windows/OpenClaw.Windows/MainWindow.cs:2241-2333`, `apps/windows/OpenClaw.Windows/MainWindow.cs:3509-3569`, `apps/windows/OpenClaw.Windows/WindowsThemePaletteResolver.cs:22-258`

## Conclusion

`apps/macos` is the more ambitious and mature desktop product surface today. It combines companion-app behavior with gateway orchestration, capability-node hosting, richer onboarding, remote topology support, voice features, and automation bridges.

`apps/windows` is a smaller but still substantial companion app. Its design is more focused: a code-built WinUI control shell with good gateway controls, realtime operator workflows, Canvas/A2UI support, and strong Windows-native device integrations. The biggest gap is not that Windows lacks the basic companion surfaces—it has them—but that macOS layers many more product capabilities on top of that baseline.
