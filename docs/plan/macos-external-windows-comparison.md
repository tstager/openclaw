# macOS vs External Windows App Comparison

Compared repositories:

- **macOS app:** `apps/macos`
- **External Windows reference app:** `openclaw-windows-node\src`

This document compares the current OpenClaw macOS app in this repo against the external Windows reference app. It focuses on **feature coverage** and **implementation differences**, not parity planning.

## Summary

The two apps are much closer in ambition than the current `apps/windows` companion is to either of them. Both are substantial desktop control surfaces with onboarding, gateway lifecycle management, realtime operator/node paths, approvals, pairing, Canvas/A2UI, diagnostics, deep links, and native integrations.

- **The macOS app is broader as a host/orchestrator.** It combines menu bar shell behavior, launchd-managed gateway supervision, remote topology and discovery, node-mode capability hosting, voice wake/talk features, cron scheduling, and a large settings/admin surface. Evidence: `apps/macos/Sources/OpenClaw/MenuBar.swift:10-160`, `apps/macos/Sources/OpenClaw/GatewayProcessManager.swift:1-447`, `apps/macos/Sources/OpenClaw/RemoteTunnelManager.swift:1-138`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeModeCoordinator.swift:5-180`, `apps/macos/Sources/OpenClaw/VoiceWakeRuntime.swift:10-140`, `apps/macos/Sources/OpenClaw/SettingsRootView.swift:1-176`
- **The external Windows app is broader as a structured Windows platform runtime.** It adds a native XAML A2UI renderer, persistent exec policy, browser proxy, MCP-only local server mode, deeper notification/history tooling, localization packs, and Command Palette integration. Evidence: `OpenClaw.Tray.WinUI/A2UI/Hosting/A2UIRouter.cs:1-117`, `OpenClaw.Shared/ExecApprovalPolicy.cs`, `OpenClaw.Shared/Capabilities/BrowserProxyCapability.cs`, `OpenClaw.Shared/Mcp/McpHttpServer.cs`, `OpenClaw.Tray.WinUI/Services/NotificationHistoryService.cs`, `OpenClaw.Tray.WinUI/Strings/en-us/Resources.resw`, `OpenClaw.CommandPalette/OpenClaw.cs`
- **The biggest differences are architectural, not just UI polish.** macOS routes more capability work through OS-native companion/runtime services, while the external Windows app routes more through a formalized node-capability, native A2UI, and WinUI/HTTP service stack. Evidence: `apps/macos/Sources/OpenClaw/PeekabooBridgeHostCoordinator.swift:8-170`, `apps/macos/Sources/OpenClaw/DeepLinks.swift:46-160`, `OpenClaw.Tray.WinUI/Services/NodeService.cs`, `OpenClaw.Shared/Mcp/McpToolBridge.cs`, `OpenClaw.Tray.WinUI/Services/SshTunnelService.cs`

## Comparison table

| Feature area | macOS app | External Windows app | Main difference |
| --- | --- | --- | --- |
| **Shell model** | Menu bar–native shell with animated critter/panel behavior | Tray/Command Center WinUI shell | macOS is more ambient and menu bar centric; Windows is more dashboard/window centric |
| **Gateway lifecycle** | launchd supervision + attach-existing + remote/direct/tailscale-aware topology | startup mode switch + SSH tunnel + operator/node/MCP-only modes | macOS is broader on discovery/topology; Windows is broader on explicit runtime modes |
| **Onboarding** | Rich multi-page wizard with CLI install and onboarding chat | Rich multi-page wizard with local/remote/configure-later and setup-code decode | roughly comparable depth, with different emphasis |
| **Realtime connection** | Strong operator/control runtime with push/event coordination | Strong operator + node runtime with more explicit mode separation | Windows is more formalized around operator vs node roles |
| **Chat / sessions** | Broader shell with chat embedded into onboarding/dashboard surfaces | WebChatWindow plus QuickSend dialog and session preview logic | Windows has more explicit quick-send/session-preview affordances |
| **Approvals / pairing** | Deep local approval engine and separate pairing prompters | Deep persistent policy plus URL navigation approval and pairing state | both are substantial; Windows policy model is more formalized |
| **Canvas / A2UI** | WKWebView-based canvas runtime and DOM bridge | Full native XAML A2UI renderer plus WebView2 canvas path | Windows is broader on native A2UI rendering |
| **Node capabilities** | Broad OS-hosted capabilities with voice/automation adjacency | Broad WinUI/HTTP node capability stack with browser proxy, TTS, MCP server | both are deep, but strongest in different areas |
| **Diagnostics / history** | Health, events, diagnostics log, broader shell coordination | Activity stream, notification history, JSONL diagnostics, port diagnostics | Windows is more structured and explicitly operational |
| **Settings / admin surface** | Much broader settings/admin center | Narrower but deeper around Windows-native runtime knobs | macOS is broader; Windows is more focused |
| **Discovery / deep links** | Discovery stack is much richer | Deep-link routing is much richer | each side is stronger in a different half of the problem |
| **Platform extras** | Voice wake/talk, cron, Peekaboo automation bridge | Command Palette extension, localization packs, MCP-only HTTP server | product-specific differentiators on both sides |

## Detailed comparison

### 1. Shell architecture and app lifetime

Both apps are OS-chrome-first rather than document-window-first, but they choose different primary shells.

- **macOS:** `OpenClawApp` is a `MenuBarExtra` app with a custom status label, hover/panel coordination, left/right click routing, and Dock visibility control. The menu bar item is the primary product entry point. Evidence: `apps/macos/Sources/OpenClaw/MenuBar.swift:10-160`
- **External Windows:** the tray app enforces single-instance startup with a named mutex, deep-link handoff, crash logging, and run-marker tracking. The shell is tray anchored but centered on windows like Command Center, Chat, Settings, and Activity. Evidence: `OpenClaw.Tray.WinUI/App.xaml.cs:28-395`

**Difference:** macOS is more menu bar-native and ambient; external Windows is more tray/dashboard-native and multi-window.

### 2. Gateway lifecycle and topology

Both apps manage the gateway directly, but their topology stories differ.

- **macOS:** `GatewayProcessManager` supervises a launchd-managed local gateway, can attach to an existing process before spawning, and coordinates `local`, `remote`, and `unconfigured` modes. It pairs with `RemoteTunnelManager`, `GatewayEndpointStore`, and Tailscale-aware discovery. Evidence: `apps/macos/Sources/OpenClaw/GatewayProcessManager.swift:1-447`, `apps/macos/Sources/OpenClaw/RemoteTunnelManager.swift:1-138`, `apps/macos/Sources/OpenClaw/TailscaleService.swift:1-182`
- **External Windows:** startup chooses between operator, node, and MCP-only modes; `SshTunnelService` handles SSH forwarding for gateway and browser-proxy ports; topology detection includes local probing and setup-code-assisted remote setup. Evidence: `OpenClaw.Tray.WinUI/App.xaml.cs:358-370`, `OpenClaw.Shared/OpenClawGatewayClient.cs:12-100`, `OpenClaw.Tray.WinUI/Services/SshTunnelService.cs`, `OpenClaw.Tray.WinUI/Onboarding/Pages/ConnectionPage.cs`

**Difference:** macOS is stronger at gateway discovery and broader topology coordination; external Windows is stronger at explicit operator/node/MCP runtime mode separation.

### 3. Onboarding

This is one of the most comparable areas between the two apps.

- **macOS:** onboarding spans CLI installation, permissions, workspace setup, gateway discovery, routing by connection mode, and onboarding chat. Evidence: `apps/macos/Sources/OpenClaw/Onboarding.swift:1-183`, `apps/macos/Sources/OpenClaw/OnboardingView+Pages.swift`, `apps/macos/Sources/OpenClaw/OnboardingView+Wizard.swift`, `apps/macos/Sources/OpenClaw/OnboardingView+Chat.swift`
- **External Windows:** onboarding spans welcome, connection, permissions, chat/node setup, and ready states, with setup-code decoding, live health checks, and local gateway approval during the wizard. Evidence: `OpenClaw.Tray.WinUI/Onboarding/`, `OpenClaw.Tray.WinUI/Onboarding/Services/GatewayHealthCheck.cs`, `OpenClaw.Tray.WinUI/Onboarding/Services/SetupCodeDecoder.cs`, `OpenClaw.Tray.WinUI/Onboarding/Services/LocalGatewayApprover.cs`

**Difference:** depth is similar; macOS leans into installation/bootstrap orchestration, while external Windows leans into operator/node mode setup and setup-code flows.

### 4. Realtime operator and node control paths

Both apps are substantial realtime clients rather than thin wrappers.

- **macOS:** `ControlChannel` coordinates realtime connectivity, gateway push subscription, work activity, event logging, and reconnection across local and remote modes. Evidence: `apps/macos/Sources/OpenClaw/ControlChannel.swift:1-449`
- **External Windows:** `OpenClawGatewayClient`, `WindowsNodeClient`, and `NodeService` split operator and node responsibilities more explicitly, with reconnecting WebSocket base classes, capability registration, pairing/auth state, and navigation-approval throttling. Evidence: `OpenClaw.Shared/WebSocketClientBase.cs`, `OpenClaw.Shared/OpenClawGatewayClient.cs`, `OpenClaw.Shared/WindowsNodeClient.cs:1-100`, `OpenClaw.Tray.WinUI/Services/NodeService.cs:1-230`

**Difference:** both are deep; external Windows is more formalized around separate roles and services, while macOS is more app-runtime-centric.

### 5. Chat and sessions

Both apps expose chat/session behavior, but external Windows is more explicit about windowed chat helpers.

- **macOS:** chat is integrated into onboarding and the broader menu bar/dashboard shell, and the app keeps more of the interaction model in the SwiftUI/AppKit product surface. Evidence: `apps/macos/Sources/OpenClaw/OnboardingView+Chat.swift`, `apps/macos/Sources/OpenClaw/MenuBar.swift:145-160`
- **External Windows:** `WebChatWindow` hosts the gateway SPA in WebView2, while `QuickSendDialog` offers lightweight composition without opening the full chat window. Session preview and switch logic are explicit in app state. Evidence: `OpenClaw.Tray.WinUI/Windows/WebChatWindow.xaml.cs:18-58`, `OpenClaw.Tray.WinUI/Dialogs/QuickSendDialog.cs`, `OpenClaw.Tray.WinUI/App.xaml.cs:79-110`

**Difference:** external Windows has more explicit chat/session helper surfaces; macOS keeps more of that flow embedded in its broader shell.

### 6. Approvals, policy, and pairing

Both apps invest heavily in local approval and pairing logic.

- **macOS:** `ExecApprovals.swift` and related helpers implement local policy, allowlist matching, blocking approval prompts over a Unix socket, and dedicated pairing prompters for device and node registration. Evidence: `apps/macos/Sources/OpenClaw/ExecApprovals.swift`, `apps/macos/Sources/OpenClaw/ExecApprovalsSocket.swift`, `apps/macos/Sources/OpenClaw/DevicePairingApprovalPrompter.swift`, `apps/macos/Sources/OpenClaw/NodePairingApprovalPrompter.swift`
- **External Windows:** `ExecApprovalPolicy.cs` persists a top-to-bottom rule file with shell filters and a policy hash, while `HttpUrlRiskEvaluator` and `UrlNavigationApprovalService` add a second approval layer for navigation/media. Pairing lives alongside operator/node/device identity flows. Evidence: `OpenClaw.Shared/ExecApprovalPolicy.cs`, `OpenClaw.Shared/ExecApprovals/`, `OpenClaw.Shared/HttpUrlRiskEvaluator.cs`, `OpenClaw.Shared/UrlNavigationApprovalService.cs`

**Difference:** both are deep, but external Windows has the more formal persistent rule-file model and extra navigation-risk policy layer.

### 7. Canvas and A2UI

This is a major implementation difference.

- **macOS:** `CanvasManager` and `CanvasWindowController` run Canvas/A2UI in a `WKWebView`, backed by a custom URL scheme, file watcher, and DOM event bridge. It is flexible and tightly integrated with the macOS shell, but still web-hosted. Evidence: `apps/macos/Sources/OpenClaw/CanvasManager.swift:1-346`, `apps/macos/Sources/OpenClaw/CanvasWindowController.swift`, `apps/macos/Sources/OpenClaw/CanvasSchemeHandler.swift`, `apps/macos/Sources/OpenClaw/CanvasA2UIActionMessageHandler.swift:1-145`
- **External Windows:** the app has both a WebView2 canvas path and a native XAML A2UI renderer with router, surface host, component registry, interactive controls, theme mapping, action transport, and secret redaction. Evidence: `OpenClaw.Tray.WinUI/A2UI/Hosting/A2UIRouter.cs`, `OpenClaw.Tray.WinUI/A2UI/Hosting/SurfaceHost.cs`, `OpenClaw.Tray.WinUI/A2UI/Rendering/ComponentRendererRegistry.cs`, `OpenClaw.Tray.WinUI/A2UI/Rendering/Renderers/`, `OpenClaw.Tray.WinUI/A2UI/Rendering/SecretRedactor.cs`, `OpenClaw.Tray.WinUI/Windows/A2UICanvasWindow.xaml.cs`

**Difference:** external Windows is clearly ahead on native A2UI rendering; macOS is still routing the experience through a browser-hosted canvas.

### 8. Native/node capability surface

Both apps expose substantial native capabilities, but with different strengths.

- **macOS:** node/runtime services cover screen capture/recording, camera, location, browser, canvas, exec approvals, and adjacent automation/voice infrastructure. Evidence: `apps/macos/Sources/OpenClaw/NodeMode/MacNodeModeCoordinator.swift:5-180`, `apps/macos/Sources/OpenClaw/NodeMode/MacNodeRuntime.swift`, `apps/macos/Sources/OpenClaw/ScreenRecordService.swift`, `apps/macos/Sources/OpenClaw/CameraCaptureService.swift`
- **External Windows:** capabilities are explicit classes under `OpenClaw.Shared/Capabilities/` and include `system`, `screen`, `camera`, `location`, `device`, `canvas`, `browser.proxy`, and `tts`, plus MCP tool bridging. Evidence: `OpenClaw.Shared/Capabilities/`, `OpenClaw.Tray.WinUI/Services/NodeService.cs:89-120`

**Difference:** both are broad. External Windows is stronger where it formalizes node capabilities into reusable classes plus MCP exposure; macOS is stronger where those capabilities are embedded into a richer host product and adjacent automation stack.

### 9. Voice, speech, and automation

This is one of the clearest macOS advantages.

- **macOS:** `VoiceWakeRuntime`, `TalkModeRuntime`, `TalkMLXSpeechSynthesizer`, overlays, and push-to-talk create a full voice feature set. `PeekabooBridgeHostCoordinator` adds a UI automation bridge over a local socket for screen, app, menu, dock, and dialog automation. Evidence: `apps/macos/Sources/OpenClaw/VoiceWakeRuntime.swift:10-140`, `apps/macos/Sources/OpenClaw/TalkModeRuntime.swift`, `apps/macos/Sources/OpenClaw/TalkMLXSpeechSynthesizer.swift`, `apps/macos/Sources/OpenClaw/PeekabooBridgeHostCoordinator.swift:8-170`
- **External Windows:** the external app has TTS via system speech and ElevenLabs, but it does not have an equivalent always-on voice wake/talk product or a Peekaboo-style automation bridge. Evidence: `OpenClaw.Shared/Capabilities/TtsCapability.cs`, `OpenClaw.Tray.WinUI/Services/TextToSpeech/TextToSpeechService.cs`

**Difference:** macOS is decisively broader in voice and automation.

### 10. Diagnostics, history, and operational tooling

External Windows is ahead here.

- **macOS:** health, diagnostics, events, and notifications exist, but they are spread across several runtime managers and windows. Evidence: `apps/macos/Sources/OpenClaw/HealthStore.swift`, `apps/macos/Sources/OpenClaw/AgentEventStore.swift`, `apps/macos/Sources/OpenClaw/DiagnosticsFileLog.swift`, `apps/macos/Sources/OpenClaw/NotificationManager.swift`
- **External Windows:** `ActivityStreamService`, `NotificationHistoryService`, `DiagnosticsJsonlService`, and `PortDiagnosticsService` create a more explicit operational toolset, with structured logs, filterable activity, notification history, and port ownership diagnostics. Evidence: `OpenClaw.Tray.WinUI/Services/ActivityStreamService.cs`, `OpenClaw.Tray.WinUI/Services/NotificationHistoryService.cs`, `OpenClaw.Tray.WinUI/Services/DiagnosticsJsonlService.cs`, `OpenClaw.Tray.WinUI/Services/PortDiagnosticsService.cs`

**Difference:** external Windows is more structured and operationally explicit; macOS has comparable signals but less concentrated operational UX.

### 11. Settings and administrative breadth

macOS is ahead here.

- **macOS:** the settings surface includes general, connection, permissions, voice, channels, skills, cron, exec approvals, sessions, instances, config, debug, and about/update flows. Evidence: `apps/macos/Sources/OpenClaw/SettingsRootView.swift:5-176`
- **External Windows:** settings are narrower but still substantial, focusing on gateway URL/token, SSH tunnel, notifications, node-mode toggles, MCP server, TTS config, and A2UI host allowlists. Evidence: `OpenClaw.Shared/SettingsData.cs`, `OpenClaw.Shared/SettingsManager.cs`, `OpenClaw.Tray.WinUI/Windows/SettingsWindow.xaml.cs`

**Difference:** macOS is the broader admin/configuration center; external Windows is narrower but more focused on WinUI/node runtime knobs.

### 12. Discovery, deep links, and platform extras

Each app is stronger in a different area.

- **macOS:** discovery is much richer thanks to `OpenClawDiscovery`, `TailscaleService`, and broader endpoint/topology management. Deep links exist and include a security model with unattended keys, but the route surface is narrower. Evidence: `apps/macos/Sources/OpenClawDiscovery/`, `apps/macos/Sources/OpenClaw/TailscaleService.swift:1-182`, `apps/macos/Sources/OpenClaw/DeepLinks.swift:46-160`
- **External Windows:** deep-link routing is much broader, with 30+ destinations including diagnostics, ports, extensibility, SSH restart, chat, dashboard paths, activity, and history. It also has platform extras like PowerToys Command Palette integration, localization packs, and the MCP-only local HTTP server. Evidence: `OpenClaw.Tray.WinUI/Services/DeepLinkHandler.cs`, `OpenClaw.Shared/DeepLinkParser.cs`, `OpenClaw.CommandPalette/OpenClaw.cs`, `OpenClaw.Shared/Mcp/McpHttpServer.cs`, `OpenClaw.Tray.WinUI/Strings/en-us/Resources.resw`

**Difference:** macOS wins on discovery; external Windows wins on deep-link breadth and Windows-specific desktop integrations.

## Conclusion

The macOS app is the broader **host/orchestrator product** today. It owns more of the gateway lifecycle, remote topology, discovery, voice interaction, automation, and admin surface.

The external Windows app is the broader **structured platform runtime**. It has a more formalized node-capability layer, richer native A2UI stack, stronger operational tooling, explicit MCP server support, broader deep-link routing, and more Windows-specific distribution/integration surfaces.

If the comparison is reduced to one line: **macOS is stronger at orchestration and product breadth; the external Windows app is stronger at structured Windows-native runtime features and operational tooling.**
