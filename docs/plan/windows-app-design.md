---
summary: Redesign target for the native OpenClaw Windows companion app shell.
title: Windows app design
read_when:
  - Designing or refactoring the native Windows companion app
  - Changing WinUI navigation, dashboard, settings, chat, pairing, approvals, or device capability UI
  - Mapping Windows app work against macOS companion app parity
---

## Status

The native Windows app already has a functional WinUI 3 first pass under
`apps/windows`. It provides a main window, tray host, Gateway lifecycle controls,
chat, approvals, pairing, device capability actions, local app preferences, MSIX
packaging, and tests.

This document is the visual and interaction target for the next Windows app
design pass. It replaces the current utility-first `TabView` shell with a
first-class desktop operations shell while keeping the existing Gateway protocol,
CLI/service contracts, and app-local preference boundaries.

## Goal

Make the Windows companion app feel like a native Windows 11 control center for
OpenClaw: compact, operational, theme-aware, and suitable for repeated daily
use. The app should help a user answer four questions quickly:

- Is my Gateway healthy?
- What are my agents and sessions doing?
- Which channels, plugins, and device capabilities need attention?
- What action can I take now?

## Non goals

- Do not copy the PulseDesk product domain, branding, fake team data, upgrade
  cards, or analytics-heavy task-management framing from the reference images.
- Do not move Gateway configuration ownership into the Windows app.
- Do not hand-edit generated protocol models under
  `apps/windows/OpenClaw.Protocol/Generated`.
- Do not redesign the Gateway protocol around Windows-only fields. Add generic
  app-facing capability or status fields only when the same shape can serve
  other native clients.
- Do not make every page a dashboard. Logs, approvals, chat, and settings need
  focused task flows more than decorative charts.

## Design Source

Use the WinUI 3 design reference images as a visual system, not a product clone.
The reusable pattern is a dense desktop productivity shell:

- A custom title bar with app identity and native Windows caption buttons.
- A persistent left navigation rail.
- A focused center workspace with a top command/search row.
- An optional right rail for context, health, alerts, and quick actions.
- Compact cards, tables, lists, chips, segmented controls, and settings rows.
- Light and dark themes with the same layout and semantic colors.

The strongest references are the design-system boards and the light/dark
Projects, Analytics, and Settings screens. For OpenClaw, the Settings references
are especially useful because they map cleanly to Gateway, runtime, model,
channel, plugin, notification, and native-device preferences.

## Product Model

Use this app-level navigation model:

- Home
- Agents
- Sessions
- Channels
- Plugins
- Devices
- Logs
- Settings

The current Status, Chat, Approvals, Pairing, Devices, and Settings tabs map
into these destinations:

- Status becomes the Home dashboard and Gateway status card.
- Chat becomes part of Sessions, with a selected session detail/chat pane.
- Approvals become Home alerts plus a dedicated Sessions or Settings subpage
  depending on context.
- Pairing becomes a Home alert, a Devices task, and a Channels setup task.
- Devices remains a primary destination for native capability health and test
  actions.
- Settings becomes a grouped settings destination rather than one flat page.

## Shell Layout

The app should use a WinUI `NavigationView` shell instead of a top-level
`TabView`.

Left navigation:

- Show the OpenClaw icon and product name near the top.
- Use compact icon plus text rows for the primary destinations.
- Keep Settings pinned near the bottom.
- Show a small Gateway state indicator near the profile/footer area.

Top command area:

- Provide a search or command box for sessions, agents, channels, plugins, and
  commands.
- Show keyboard shortcut text in the box when available.
- Include primary quick action as a split button, such as New session, Add
  channel, or Install plugin depending on the current page.
- Use icon buttons for notifications, help, and refresh.

Right rail:

- Make the rail optional and collapsible.
- Use it for Gateway health, recent events, pending approvals, pairing requests,
  and quick repair actions.
- Do not use it for ads, upgrade prompts, or decorative content.

Responsive behavior:

- At wide widths, show left nav, main workspace, and right rail.
- At medium widths, collapse the right rail behind an icon button or side pane.
- At narrow widths, compact the left nav and prefer task-focused pages over
  dense dashboards.

## Theme And Tokens

Use WinUI theme resources first, with OpenClaw-specific semantic aliases only
where the app needs consistent status meaning.

Typography:

- Segoe UI Variable.
- Page title: 24 to 32 px, semibold.
- Section heading: 18 to 20 px, semibold.
- Body: 14 px with 20 px line height.
- Caption and metadata: 12 px with 16 px line height.

Spacing:

- Use a 4 px base scale: 4, 8, 12, 16, 24, 32, 48, 64.
- Keep card and panel padding at 16 or 24 px depending on density.
- Use tighter spacing inside repeated rows and list items.

Corners and elevation:

- Use WinUI corner resources such as `ControlCornerRadius` and
  `OverlayCornerRadius` rather than hardcoded radii where a native control
  already provides the shape.
- Use an 8 px visual target for custom cards, 4 px for tight custom surfaces,
  and 12 px for larger custom panels when no WinUI resource applies.
- Use pill corners only for chips, status pills, toggles, and avatars.
- Prefer borders and subtle material separation over heavy shadows.

Color:

- Use WinUI theme brushes first.
- Map OpenClaw semantic aliases to theme-aware resources rather than literal
  colors.
- Primary action: blue family.
- Secondary emphasis: violet family.
- Informational accent: cyan family.
- Success: green family.
- Warning: amber family.
- Error: red family.
- Keep semantic meaning identical in light and dark themes.

Dark theme:

- Use near-black or deep neutral backgrounds, not pure black.
- Use translucent dark surfaces with visible borders.
- Keep text contrast high enough for logs and diagnostics.

Light theme:

- Use near-white page background with white raised surfaces.
- Use soft blue selected navigation tint.
- Keep borders visible but quiet.

## Core Components

Standard components:

- Navigation rail.
- Top app bar.
- Search or command box.
- Primary split button.
- Icon buttons.
- KPI cards.
- Status chips.
- Progress bars.
- Segmented controls.
- Tabs inside a page only when they organize a single destination.
- Dropdowns.
- Toggles.
- Checkboxes and radio buttons.
- Compact data tables.
- Activity lists.
- Toast notifications.
- Side panels.
- Modal confirmation dialogs.

OpenClaw-specific components:

- Gateway status card: running, stopped, degraded, unreachable, service mode,
  endpoint, and last check.
- Agent card: name, model, workspace, channel bindings, current activity, and
  quick actions.
- Session row: title, agent, channel, last message, token/cost summary when
  available, and open action.
- Channel card: plugin name, account, connection state, auth state, last event,
  and repair action.
- Plugin card: installed state, version, capabilities, health, and update or
  uninstall action.
- Approval card: command, source agent/session, risk summary, allow once, deny,
  and details.
- Pairing request card: requester, channel/device, trust context, approve, deny,
  and expire.
- Device capability card: screen, camera, microphone, hotkey, notifications,
  overlay, and permission or test action.
- Log/event row: severity, timestamp, source, message, and copy/open action.
- Model/provider badge: provider, model, auth profile state, and fallback state.
- Confirmation dialog: use WinUI `ContentDialog` and set `XamlRoot` from the
  window content/root element before showing it.

## Home

Home is the first screen and should replace the current Status tab as the
default operational dashboard.

Top KPI cards:

- Gateway status.
- Active sessions.
- Connected channels.
- Pending approvals.
- Device capability health.

Main panels:

- Recent activity: session events, channel events, approvals, pairing requests,
  and Gateway lifecycle events.
- Agents and sessions: compact list of recently active agents and sessions.
- Channel health: connected, degraded, needs auth, or disabled.

Right rail:

- Gateway health summary.
- Pending approval and pairing request stack.
- Quick actions: start Gateway, restart Gateway, open logs, new session, add
  channel, install plugin.

## Agents And Sessions

Agents should focus on configuration and state; Sessions should focus on active
conversation and transcript work.

Agents page:

- Agent list with model, workspace, channel bindings, and status.
- Selected agent detail with identity, model/provider, tools, bindings, and
  quick actions.
- Use cards for repeated agents and a detail pane for editing or inspection.

Sessions page:

- Session list with filters for agent, channel, active, recent, and errored.
- Chat/detail pane with message bubbles, tool-call blocks, attachments, and
  status metadata.
- Composer with send, stop, retry, and attach actions where supported.
- Preserve the current `chat.history` and `chat.send` flow, but present it as a
  native chat surface instead of a raw event list.

## Channels And Plugins

Channels and plugins should feel like configuration inventory and health
surfaces, not analytics pages.

Channels page:

- Card or table view with channel plugin, account, auth state, connection state,
  last inbound event, and repair action.
- Pairing and access-control prompts should appear inline when a channel needs
  setup.
- Use status chips for connected, degraded, needs auth, disabled, and unsupported
  on this host.

Plugins page:

- Installed plugins list with version, enabled state, capabilities, and health.
- Marketplace or install entry point if available through existing CLI/Gateway
  flows.
- Keep plugin terminology in UI and docs. Use repo path names only when
  explaining internals.

## Devices

Devices owns native Windows capability status and test actions.

Show one card per capability:

- Screen capture.
- Camera.
- Microphone.
- Push-to-talk hotkey.
- Notifications.
- Overlay windows.

Each card should show:

- Permission state.
- Last successful test.
- Current adapter availability.
- Primary action such as Grant, Test, Capture, Show overlay, or Open settings.
- Failure detail and repair guidance when blocked.

Use confirmation dialogs for actions that expose screen, camera, microphone, or
execution-sensitive context.

In WinUI desktop code, confirmation dialogs should be `ContentDialog` instances
with `XamlRoot` set from the active window content/root element before display.

## Logs

Logs should be a first-class destination, not just an Open Logs button.

Core behavior:

- Show recent Gateway and app events in a compact table.
- Filter by severity, source, and text.
- Provide copy, reveal file, and refresh actions.
- Show clear empty, loading, and unreachable states.
- Keep raw logs readable in both themes.

## Settings

Settings should follow the grouped horizontal row pattern from the reference
images.

Recommended groups:

- Appearance: theme, density, accent color if the app supports it.
- Gateway: URL, token source, service mode, startup behavior, and lifecycle
  controls.
- Agents and sessions: default agent, default session, and chat behavior.
- Channels and plugins: links to their inventory pages plus update or repair
  preferences.
- Notifications: toast behavior, approval prompts, pairing requests, and health
  alerts.
- Devices: capability permissions and test shortcuts.
- Storage and logs: app preference path, Gateway state note, log path, and
  cleanup actions.
- About: version, protocol version, build metadata, update state, and links to
  Windows docs.

Settings must remain app-local except where an action intentionally opens or
delegates to existing Gateway configuration flows.

## Tray And Notifications

The tray surface should be the compact version of Home:

- Gateway state.
- Start, stop, restart.
- Open app.
- Pending approval count.
- Pending pairing count.
- Recent warning if present.
- Quit.

Notifications should be actionable when Windows supports it:

- Approval requested.
- Pairing requested.
- Gateway stopped or degraded.
- Channel needs auth.
- Device permission blocked.

Notification content must be concise and avoid exposing secrets.

## Migration From Current Shell

The current programmatic `MainWindow` can migrate incrementally:

1. Introduce the `NavigationView` shell and keep existing panels as temporary
   destination content.
2. Move Status content into Home and Gateway cards.
3. Move Chat content into Sessions.
4. Move Approvals and Pairing into reusable cards that can appear on Home and
   their destination pages.
5. Move Devices into capability cards.
6. Replace the flat Settings panel with grouped settings rows.
7. Add Logs as a first-class destination.

This keeps current Gateway actions and protocol calls working while improving
navigation and presentation.

## Accessibility And Localization

- Every icon-only button needs an accessible name and tooltip.
- Keyboard navigation must work across nav, command box, cards, lists, dialogs,
  and chat composer.
- Status must not rely on color alone. Pair color with text and iconography.
- Use WinUI high contrast resources where possible.
- Keep strings centralized enough that later localization work does not require
  redesigning the UI.

## Test Plan

Design implementation should be covered with the existing Windows app test
commands plus targeted state or view-model tests where practical. The current
Windows test project is a plain MSTest project, so it should not instantiate
WinUI controls directly. Actual XAML UI tests require a separate desktop UI
automation path or a dedicated WinUI test host.

```powershell
pnpm windows:protocol:check
pnpm windows:test
pnpm windows:build
```

Manual smoke on Windows:

- Launch the app from a source checkout.
- Confirm Home shows accurate Gateway state.
- Start, stop, and restart the Gateway from the app.
- Open Sessions and send a chat message.
- Approve and deny a pending approval.
- Approve and reject a pairing request.
- Run each device capability test after permission grants.
- Switch light and dark themes and inspect Home, Sessions, Devices, Logs, and
  Settings.
- Relaunch and confirm app-local preferences persist.

## Acceptance Criteria

- The first screen is Home, not a raw tab strip.
- Primary navigation uses WinUI shell navigation with stable destinations.
- The right rail is useful operational context and can collapse.
- All current functional surfaces remain reachable.
- Gateway config remains Gateway-owned.
- Light and dark themes are both complete.
- Status, errors, and approvals are readable without relying only on color.
- The app can be operated primarily by keyboard.
- Windows UI changes do not require hand-editing generated protocol code.
