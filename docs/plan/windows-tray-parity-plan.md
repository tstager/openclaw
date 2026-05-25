# Windows Tray Parity Plan

## Goal

Bring the OpenClaw Windows companion tray experience up to the useful tray functionality demonstrated by the external `openclaw-windows-node` reference project, while keeping the current app architecture and avoiding a wholesale port.

## Reference Findings

The current Windows companion already has tray lifetime, gateway controls, notifications, themes, Canvas/A2UI, diagnostics, and app navigation. The external reference goes further in the tray area with:

- a custom WinUI tray flyout instead of a simple WinForms context menu
- live gateway, node, session, usage, and activity rows
- status dots, compact badges, hover/flyout details, and dark/light-aware styling
- quick actions for dashboard, chat, canvas, setup, settings, support, logs, and exit
- permission toggles for node capabilities
- richer tooltip text and diagnostics entry points

The current tray baseline is `apps/windows/OpenClaw.Windows.Native/WindowsTrayHost.cs`, which owns a WinForms `NotifyIcon` and `ContextMenuStrip`. It is wired from `apps/windows/OpenClaw.Windows/App.cs` and calls back into `MainWindow` for actions.

## Design Direction

Keep `WindowsTrayHost` as the native `NotifyIcon` bridge because WinUI 3 has no built-in system tray API. Move visible tray UX toward a small WinUI flyout window driven by a snapshot model. The flyout should use the app's existing theme, accent, navigation, preferences, gateway state, and diagnostics services instead of copying the reference app's separate architecture.

The first tray parity milestone should not add full-node capabilities that are not yet implemented. It should expose current capabilities clearly and leave full-node command parity for the upcoming full-node sessions.

## Session Plan

### Session 1: Tray State Model

Add a tray snapshot/action model in `apps/windows/OpenClaw.Windows`.

Capture:

- gateway status and URL
- realtime connection state
- latest activity
- session count
- pending approvals and pairing counts
- Canvas/A2UI node readiness
- notification state
- theme, accent, and color theme preferences
- available gateway lifecycle actions

Add focused unit tests for snapshot building so the tray can update without reading directly from `MainWindow`.

### Session 2: WinUI Tray Flyout

Keep `WindowsTrayHost` as the `NotifyIcon` owner, but change it from owning the whole tray menu to raising tray open/click events.

Add a compact WinUI flyout window that follows the reference PNG direction:

- dark/light aware
- status-dot rows
- compact separators
- icon plus label action rows
- right-aligned badges
- no large dashboard cards inside the tray

Preserve double-click-to-open and explicit exit behavior.

### Session 3: Live Status Rows

Implement flyout sections for:

- Gateway: URL/host, connected/disconnected state, local/remote indicator
- Windows node/Canvas: paired, ready, pending, disabled, or unavailable
- Sessions: active/session count when known
- Usage: shown only when reliable usage data exists
- Activity: latest meaningful app/gateway activity

Update the tray tooltip to include gateway state, node/canvas state, warning count, and latest activity.

### Session 4: Quick Actions

Add tray actions for:

- Home
- Dashboard
- Chat
- Canvas
- Sessions
- Approvals
- Pairing
- Logs
- Settings
- Connect or disconnect
- Install, start, restart, and stop gateway
- Exit

Common failure paths should route visibly to Settings or Logs rather than silently doing nothing.

### Session 5: Permissions And Toggles

Add a tray permissions section for current implemented local capabilities:

- Canvas/A2UI node
- notifications
- voice controls if still present
- screen, camera, and device capability controls where backed by existing preferences or real Windows capability state

Do not expose full-node command toggles until the full-node sessions add the real capability backend.

### Session 6: Activity And Support

Add tray entry points for:

- notification history
- activity history
- support summary
- crash log
- app log folder
- gateway log folder
- diagnostics/support artifact creation

Reuse existing stores and diagnostics. Do not copy the external project's independent diagnostics pipeline unless a later plan chooses that as a separate architecture change.

### Session 7: Visual Polish And VM Smoke

Polish the flyout against the reference PNGs:

- compact dark flyout
- status dots
- right-aligned badges
- icon rows
- stable sizing and positioning near the tray icon
- readable light, dark, and system themes
- accent color applied to selected or active affordances

Verify with focused tests and a VM smoke:

- tray opens and closes reliably
- double-click opens the app
- exit disposes the tray icon
- actions route to the expected pages
- theme switching does not create unreadable flyout colors
- no blank or black tray/flyout window appears

## Future Work After Tray Parity

The external reference also points to future work that should stay separate from this tray plan:

- full Windows node capability parity
- gateway-side `openclaw-windows` client support
- local MCP and node command surfaces
- PowerToys Command Palette integration
- richer onboarding/setup wizard
- WSL/local gateway installer
- structured JSONL diagnostics with rotation
- deep link registration and IPC routing
- richer security/sandbox policy UI

The next major implementation track should be the full-node sessions, using the saved full-node scopes plan as the starting point.
