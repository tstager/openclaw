# OpenClaw Windows companion software manual

This manual explains how the OpenClaw Windows companion app works, how it connects to an OpenClaw Gateway, what each page in the app is for, and how to configure the settings that control its behavior. It is written for operators who want to run the Windows app against a local or remote gateway and use it as a native control surface for chat, approvals, pairing, diagnostics, and Windows-specific capabilities.

The Windows companion is a **dual-role client**, not a second gateway. It connects to the gateway as both an **operator UI** (requesting the full operator scope set over its `openclaw-windows` client id) and a **Windows-native capability node** (advertising Canvas/A2UI, screen capture, camera, and — when explicitly enabled — secure system execution). The gateway remains the source of truth for sessions, approvals, pairing, channel connections, model/provider auth, the Control UI, and the OpenClaw HTTP and WebSocket APIs. The Windows app stores local preferences and Windows-specific runtime data, then uses the gateway's WebSocket and CLI surfaces to operate the system. See [Full node mode, operator scopes, and secure execution](#full-node-mode-operator-scopes-and-secure-execution) for the node and scope details.

## What the Windows companion owns

The Windows companion owns:

- the native WinUI shell and navigation
- the local chat workspace for one selected OpenClaw session
- Windows-only features such as local screen capture, bounded screen recording, notifications, overlays, global hotkeys, and local speech clip generation
- the Windows-native capability node: Canvas/A2UI, screen snapshot and recording, and camera commands exposed to the gateway through `node.invoke`
- the local execution policy that gates secure system execution (`system.which`, `system.run.prepare`, `system.run`) behind explicit enablement
- app-local preferences, activity history, notification history, and structured diagnostics
- operator views for approvals, pairing, sessions, logs, and onboarding status

The Windows companion does **not** own:

- gateway configuration schema or channel configuration
- model/provider authentication on the gateway host
- channel connections, pairing policy, or session lifecycle
- the browser Control UI served by the gateway

## How the app and gateway work together

At runtime, the integration looks like this:

1. The gateway runs as the always-on OpenClaw control plane.
2. The Windows app stores the gateway URL, shared token, chat session key, and local UI preferences.
3. When you choose **Connect**, the app saves settings, opens a WebSocket to the gateway, and requests operator scopes.
4. After connection, the app loads chat history, sessions, approvals, and pairing requests from the gateway.
5. If Canvas is enabled, the app also connects a Windows Canvas node back to the gateway so the gateway can drive the native A2UI surface.
6. Windows-only features such as screen capture, notifications, overlays, hotkeys, local speech clip export, and local diagnostics stay on the Windows machine and are surfaced through the app.

The practical split is:

- **gateway**: state, auth, channels, sessions, approvals, pairing, HTTP APIs, dashboard
- **Windows companion**: operator UX plus Windows-native device features

## Full node mode, operator scopes, and secure execution

The Windows companion connects to the gateway twice: once as an operator UI socket and once as a capability node socket. Both use the first-class `openclaw-windows` gateway client id.

### Operator scopes

On the operator socket the app requests the complete operator scope set: `operator.read`, `operator.write`, `operator.admin`, `operator.approvals`, `operator.pairing`, and `operator.talk.secrets`. The gateway is default-deny on scopes and only grants the scopes that are bound to the paired device, so privileged scopes such as `operator.admin` and `operator.talk.secrets` may not be granted to a freshly paired device.

The app surfaces the result honestly:

- **Home** shows a **Scopes** row summarizing granted vs. missing scopes.
- **Settings > Gateway Connection** lists the requested scopes and the granted/missing summary.
- **Pairing** shows the capability, granted scopes, missing scopes, the Windows node state, and the pairing requirement, with a **Repair access** button.

If a device is stuck on stale narrow scopes (connected but missing baseline operator scopes), use **Repair access** on the Pairing page. It resets the local device identity and reconnects so the gateway can re-issue access at the current pairing's scope level.

### Windows node capabilities

When **Canvas and A2UI node** is enabled (Settings > Devices), the node advertises its capabilities and commands to the gateway, derived from the host and your preferences:

- **caps**: `canvas`, `screen`, `camera`
- **Canvas/A2UI commands**: `canvas.present`, `canvas.hide`, `canvas.navigate`, `canvas.eval`, `canvas.snapshot`, `canvas.a2ui.push`, `canvas.a2ui.pushJSONL`, `canvas.a2ui.reset`
- **device commands**: `screen.snapshot`, `screen.record`, `camera.list`, `camera.snap`

The screen and camera commands run through the same native services as the Devices page; results return structured `node.invoke` payloads with file metadata, and failures return structured errors (`INVALID_REQUEST` for bad params, `UNAUTHORIZED` for denied consent, `UNAVAILABLE` for missing devices). `screen.record` and `camera.snap` are high-risk node commands, so the gateway only routes them when they are allowlisted in `gateway.nodes.allowCommands`.

### Secure system execution

System execution is **off by default**. Enable **Allow secure system execution (system.run)** in Settings > Devices to advertise `system.which`, `system.run.prepare`, and `system.run`. An optional local allowlist restricts which executables may run; an empty allowlist permits any executable once execution is enabled. Each run returns a `node.invoke` result and emits `exec.finished` (or `exec.denied`) node events carrying the `runId` and `sessionKey`.

This local enablement is defense in depth. On the gateway side, `system.run`, `system.run.prepare`, and `system.which` are desktop host commands that require **node pairing**, and approving the pairing of a node that advertises system execution requires an operator with **both** `operator.pairing` and `operator.admin`. The Pairing page explains this requirement when the node advertises system execution.

### Node topology in the tray

The tray flyout shows a **Nodes** section with one row per node — this Windows node plus any remote nodes the gateway reports — each with an online/paired role line and a platform badge. The Gateway status row's detail is annotated with the connected/paired node counts, and the flyout header's master toggle enables or disables the Windows node.

## Recommended deployment patterns

### Local same-machine gateway

This is the simplest setup.

- Run the gateway on the same Windows machine as the app.
- Use the default URL `ws://127.0.0.1:18789`.
- Save the gateway token in the app's **Gateway Connection** card.
- Use the Home page to install, start, restart, stop, and connect.

### Remote gateway over SSH tunnel

This is the safest remote pattern when the gateway host is not on the same machine.

- Keep the gateway loopback-bound on the remote host.
- Open an SSH tunnel from Windows to the remote gateway host.
- Keep the app's **Gateway URL** set to a local forwarded URL such as `ws://127.0.0.1:18789`.
- Configure the SSH host and forwarded ports in **Settings > Topology and Tunnels**.

Recommended tunnel shape:

```powershell
ssh -N -L 18789:127.0.0.1:18789 <user>@<gateway-host>
```

The app's tunnel settings automate the same idea: a local port forwards to the remote host and port after SSH connects.

### Direct private-network gateway

If the gateway is intentionally reachable on a trusted LAN or tailnet:

- set the app's **Gateway URL** to the private gateway WebSocket URL
- keep gateway auth enabled
- use a shared token or other supported auth mode on the gateway

Avoid exposing the gateway or dashboard publicly. For remote access, prefer Tailscale or SSH rather than broad public ingress.

## Quickstart

### 1. Prepare the gateway host

Install OpenClaw on the host that will run the gateway. Then choose one of these startup paths:

```powershell
openclaw onboard
```

or

```powershell
openclaw gateway install
openclaw gateway status
```

For a one-off foreground run:

```powershell
openclaw gateway --port 18789
```

Verify healthy status:

```powershell
openclaw status
openclaw gateway status
openclaw logs --follow
openclaw doctor
```

Healthy signals include a running runtime, a reachable RPC surface, and the expected capability level.

### 2. Configure model/provider auth on the gateway host

The Windows app does not configure model API keys. Put provider credentials on the **gateway host**.

Example pattern:

```powershell
setx OPENAI_API_KEY "<API_KEY>"
setx ANTHROPIC_API_KEY "<API_KEY>"
```

For long-lived gateway installs, storing credentials in the gateway host's `~/.openclaw/.env` or through `openclaw onboard` is usually the most predictable approach.

Helpful references:

- https://docs.openclaw.ai/gateway/authentication
- https://docs.openclaw.ai/gateway/configuration

### 3. Launch the Windows companion

Open the Windows app and go to **Settings** first.

In **Gateway Connection**:

- set **Gateway URL**
- set **Gateway token** if the gateway uses shared-token auth
- click **Save**

In **Identity**:

- set **Chat session** to the session key the Chat page should use by default

Then go to **Home** and click **Connect**.

### 4. Verify the integration

After connection:

- **Home** should show gateway, health, and connection summaries
- **Chat** should load history for the selected session
- **Sessions** should list gateway sessions
- **Approvals** and **Pairing** should load current operator queues
- **Devices** should show Windows capability status

## Gateway setup for the Windows companion

The Windows app works best when the gateway is configured with these expectations:

### Gateway URL and port

The app expects a gateway WebSocket URL. The default is:

```text
ws://127.0.0.1:18789
```

If you change the gateway port, update the app's **Gateway URL** to match.

### Gateway auth

The app's primary connection path is a gateway shared secret token stored in the app's settings and persisted through the Windows credential store when available. The app also caches a device token after pairing so reconnects can use the approved device identity.

If you use:

- **shared token auth**: save the token in **Settings > Gateway Connection**
- **password auth**: use the gateway's password path where appropriate
- **trusted proxy / Tailscale identity**: keep the gateway configured for that environment, but remember the Windows app still needs to reach the WebSocket on a trusted path

Useful dashboard and auth references:

- https://docs.openclaw.ai/web/dashboard
- https://docs.openclaw.ai/gateway/authentication
- https://docs.openclaw.ai/gateway/remote

### Gateway service lifecycle

The Windows app's Home quick actions map to the normal gateway CLI lifecycle:

- **Install** → `openclaw gateway install`
- **Start** → `openclaw gateway start`
- **Restart** → `openclaw gateway restart`
- **Stop** → `openclaw gateway stop`

These actions operate the real gateway service; they are not app-local toggles.

### Pairing and approvals

Approvals and pairing only become useful after the app establishes the realtime WebSocket connection. The gateway remains the source of truth for:

- pending command approvals
- pending device or node pairing requests
- device tokens issued after approval

### Remote gateway setup

If the gateway is remote, you generally have two good choices:

1. **SSH tunnel** and keep the app pointed at `ws://127.0.0.1:<local-port>`
2. **Private direct URL** on LAN or tailnet with gateway auth enabled

For Windows companion operators, SSH is the most universal fallback because it preserves the local loopback mental model inside the app.

## Section-by-section app guide

### Home

The **Home** page is the operator dashboard.

It shows:

- **Gateway** summary card: service state and RPC status
- **Health** summary card: onboarding readiness
- **Connection** summary card: realtime connection state
- **Gateway status** details: service, RPC reachability, capability, dashboard URL, log path
- **Connection state** details: realtime state, current endpoint, last connection detail
- **Quick actions**: Install, Start, Restart, Stop, Connect, Open Logs
- **Operator workflows**: approvals status, pairing status, overall readiness
- **Onboarding health**: prerequisite checks
- **Guided next steps**: recommended actions based on current gateway, tunnel, browser proxy, and speech status
- **Recent activity** and **Notification activity**

Use Home when you need to answer these questions quickly:

- Is the gateway installed?
- Is the gateway reachable?
- Is the app connected to realtime?
- What should I fix next?

### How Home is configured

Home is driven by:

- the saved **Gateway URL**
- the saved gateway token
- the current gateway status from the CLI
- the realtime connection state
- onboarding checks
- tunnel status
- browser proxy readiness
- Windows speech availability

If Home says the gateway is reachable but realtime is disconnected, click **Connect**. If the gateway is not reachable, use the quick actions first.

### Chat

The **Chat** page is a native session-scoped conversation surface.

It includes:

- the current session header
- a status bar showing connection and send state
- a scrollable transcript
- a composer with **Send**
- **Refresh** for reloading history

### How Chat works

- The app loads messages from the gateway for the selected session key.
- Sending a message posts the text to the gateway for that session.
- The default session comes from **Settings > Identity > Chat session**.
- The active session can also be switched from the **Sessions** page.

### How to configure Chat

In **Settings > Identity**, set **Chat session** to the default session key, such as `main`.

Use Chat when you want a native operator conversation against one existing gateway session without opening the browser dashboard.

### Canvas

The **Canvas** page hosts the Windows A2UI surface in a WebView2 container.

It includes:

- **Connect Canvas**
- **Refresh A2UI**
- live status and detail text
- the embedded Canvas/A2UI web surface

### How Canvas works

Canvas is not just a browser view. The Windows app can register a **Canvas node** back to the gateway. When enabled:

- the app reconnects the Windows Canvas node after realtime connect
- the gateway can drive A2UI presentation through the trusted Canvas host
- URL navigation is restricted by policy and trusted A2UI host rules

### How to configure Canvas

Enable **Settings > Devices > Enable Canvas and A2UI node**.

If Canvas is disabled, the page will report that the Canvas node is disabled and will not expose the A2UI surface to the gateway.

Use Canvas when the gateway or agent workflow depends on the Windows A2UI host.

### Sessions

The **Sessions** page is a browser for gateway sessions.

It shows:

- session display name
- session key
- kind
- agent
- channel
- state
- last updated time
- a button to make a session active in Chat

### How Sessions works

Sessions are fetched from the gateway over realtime RPC. The Windows app does not own session lifecycle; it only displays and selects sessions.

### How to use Sessions

- click **Refresh** to reload sessions
- click **Use in Chat** to make that session the active Chat session

This updates the stored **Chat session** preference and jumps you to the Chat page.

### Approvals

The **Approvals** page is the operator queue for command approvals requested by the gateway.

Each approval card shows:

- command text
- approval id
- working directory when available
- agent id when available
- session key when available
- a derived **Risk** label

Actions:

- **Allow once**
- **Allow & remember** when the default policy is **Ask every time**
- **Deny**

### How Approvals works

Approvals are fetched from the gateway. The app can also auto-resolve them locally according to the saved approval policy:

- **Ask every time**
- **Allow safe commands automatically**
- **Deny risky commands automatically**

If you use **Allow & remember**, the command is added to the local remembered allowlist in policy settings.

### How to configure Approvals

Use **Settings > Approval Policy** to control:

- default auto-resolution behavior
- unsafe URL blocking
- diagnostic redaction

Approvals are useful only when the gateway session policy actually emits approval requests.

### Pairing

The **Pairing** page is the operator queue for device or node pairing requests.

Each pairing card shows:

- display name
- kind
- device id
- request id

Actions:

- **Approve**
- **Reject**

### How Pairing works

The gateway creates and owns pairing requests. The Windows app is a frontend for resolving them.

Approve when you trust the requesting device or node. After approval, the gateway issues a token and the device reconnects with that identity.

The Pairing page also shows an **Operator and node access** card with the current capability, granted scopes, missing scopes, the Windows node state, and the pairing requirement, plus a **Repair access** button. Approving a node that advertises secure system execution requires an operator with both `operator.pairing` and `operator.admin`. See [Full node mode, operator scopes, and secure execution](#full-node-mode-operator-scopes-and-secure-execution).

### When to use Pairing

Use Pairing whenever:

- a new device needs access
- a node requests trust
- a device token has been rotated or revoked and must be reapproved

Reference: https://docs.openclaw.ai/gateway/pairing

### Devices

The **Devices** page is the Windows-native capability dashboard.

It combines host capability state, local actions, and gateway-dependent readiness checks.

Current capability cards include:

- Screen
- Screen recording
- Camera
- Microphone
- Hotkeys
- Notifications
- Browser proxy
- System speech
- Overlays

### Screen

Captures a single screenshot of the primary display and saves it under the app's capture directory.

### Screen recording

This is a **bounded frame capture** workflow, not a long-running video encoder. The app:

- captures a short sequence of PNG frames
- clamps duration to 30 seconds maximum
- clamps frame rate to 10 fps maximum
- shows a recording plan preview before capture
- writes frames into a timestamped folder

Use it for quick operator proofs, not for production-grade video recording.

### Camera

Uses Windows camera capture APIs and the normal Windows consent flow to save a still image.

### Microphone and voice controls

The device card reports microphone availability and lets you save the local **Enable voice controls** preference. Voice controls depend on Windows audio device access.

### Hotkeys

Lets you save the **Register Ctrl+Shift+Space push-to-talk hotkey** preference. Global hotkeys are only registered while enabled.

### Notifications

Lets you send a test notification and verify that the Windows notification surface is working.

### Browser proxy

This card currently reports **browser proxy readiness**, not a full embedded browser-control workflow. It tells you whether:

- the host advertises browser proxy capability
- the saved gateway URL is valid
- the gateway is reachable
- the current URL safety policy is compatible

Use **Open settings** to adjust gateway URL or policy, and **Open dashboard** to jump to the gateway Control UI.

### System speech

The Windows app can save a local speech clip using installed Windows voices.

Important behavior:

- it saves clips to files instead of auto-playing them
- you can pick a voice or leave the default selected
- if no compatible speech components or voices are installed, the feature shows as unavailable

### Overlays

Shows a test app-owned overlay window to verify the overlay surface.

### Logs

The **Logs** page is the app's local diagnostics and support workspace.

It includes:

- diagnostics summary
- location cards for the app crash log and gateway log
- recent activity history
- notification history
- support summary
- gateway event visibility controls
- filtered gateway events
- raw log preview

### Diagnostics

The diagnostics summary shows:

- gateway state
- last error
- last refresh time
- structured diagnostics path
- activity history path
- notification history path

### Locations

For both app and gateway logs, the page supports:

- **Copy path**
- **Reveal file**
- **Open folder**

### Recent activity history

Shows recent local operator activity recorded by the app, such as settings saves, reconnects, approvals, and pairing actions.

### Notification history

Shows persisted notification entries after notification rules have classified them by category and destination.

### Support summary

The support summary is an operator-friendly snapshot of:

- gateway URL
- diagnostics enablement and path
- activity history path and retention
- notification history path and retention
- recent activity
- recent notifications
- stored notification rules

Use:

- **Copy support summary** for pasteable text
- **Save support artifact** for a JSON artifact written near the app's local history store

### Gateway events

The page also exposes realtime gateway event filtering so you can:

- show all events
- hide operational noise
- show chat-only events
- reset filters

This is useful when debugging why the gateway is or is not updating the Windows UI.

### Settings

The **Settings** page is where app-local configuration is edited and persisted.

Click **Save** to persist changes. Saving settings immediately refreshes the app state.

### Gateway Connection

#### Gateway URL

- what it does: sets the realtime WebSocket endpoint the app will connect to
- default: `ws://127.0.0.1:18789`
- use it for: local gateway, direct private gateway, or SSH-forwarded loopback URL

#### Gateway token

- what it does: stores the shared gateway token used for gateway CLI probes and realtime connection
- storage: persisted through the app preference store, delegating secrets to the Windows credential store when available

### Identity

#### Chat session

- what it does: sets the default OpenClaw session key used by the native Chat page
- default: `main`

### Appearance

#### Theme

Options:

- System
- Light
- Dark

This changes the app's shell theme behavior.

#### Accent color

Options:

- System
- Blue
- Teal
- Green
- Orange
- Rose
- Purple

This changes the app-owned accent/highlight color.

#### Color theme

Options:

- Default
- Slate
- Forest
- Ocean
- Ember
- High Contrast

This changes the app-owned surface palette.

### Startup

#### Open main window on launch

- what it does: controls whether the main window opens on launch

#### Reserved items

The page also shows reserved rows for future settings:

- Autostart
- Minimize to tray
- Tray quick actions

Treat those as placeholders, not active runtime controls.

### Notifications

#### Alert toggles

You can enable or disable app notifications for:

- Approval alerts
- Pairing alerts
- Gateway health alerts
- Device permission alerts

#### Notification history retention

- what it does: caps the number of persisted notification history entries
- default: `100`

#### Notification rules

Notification rules let you edit, per notification kind:

- whether the rule is enabled
- the stored category label
- the destination page opened when selected from history or the tray

Default destinations are:

- approval → Approvals
- pairing → Pairing
- gateway health → Home
- device permission → Devices

### Topology and Tunnels

These settings support remote-gateway workflows.

#### SSH host

The SSH target used when the app starts a tunnel.

#### Remote host

The host forwarded after SSH connects. Default is `127.0.0.1`.

#### Local port

The local forwarded listener used by the Windows app.

#### Remote port

The gateway port on the remote side.

#### Auto-start SSH tunnel

When enabled, the app treats the tunnel as expected runtime topology and uses it in guided onboarding and topology health.

#### Tunnel actions

- **Start tunnel**
- **Stop tunnel**
- **Refresh topology**

Use this card when the gateway is remote and the app should work through SSH forwarding.

### Diagnostics and History

#### Write structured diagnostics

Enables JSONL structured diagnostic output from the Windows companion.

#### Diagnostics path

Overrides the structured diagnostics path. If blank, the app uses its default diagnostics file path.

#### History retention

Sets the maximum persisted activity row count. Default is `200`.

### Approval Policy

#### Default policy

Options:

- Ask every time
- Allow safe commands automatically
- Deny risky commands automatically

#### Block unsafe URLs

Blocks URLs that fail the local URL safety evaluator. This affects features such as Canvas navigation and browser-proxy-related flows.

#### Redact sensitive content before saving diagnostics

Controls whether sensitive content is redacted before local diagnostics are persisted.

### Devices

#### Enable Canvas and A2UI node

Controls whether the Windows Canvas node is exposed back to the gateway.

#### Enable voice controls

Controls the local voice-controls preference.

#### Register Ctrl+Shift+Space push-to-talk hotkey

Controls whether the app should register the global push-to-talk hotkey.

#### Allow secure system execution (system.run) behind node pairing

Controls whether the Windows node advertises and runs the secure system execution commands. It is off by default. See [Secure system execution](#secure-system-execution) for the pairing and admin requirements.

### Runtime feature storage

This card is informational. It shows the active runtime paths or latest status text for:

- Captures
- Speech clips
- Browser proxy readiness/result text

### Storage and Logs

This card is informational and shows the current paths for:

- preferences JSON
- app crash log
- gateway log
- activity history
- notification history
- structured diagnostics
- captures
- speech clips
- latest support artifact, when one has been saved

### About

Shows:

- product name
- gateway protocol version
- a runtime summary of the saved settings

## How to configure common workflows

### Basic local operator setup

Use this when the gateway runs on the same Windows machine.

1. Install or start the gateway.
2. Verify `openclaw gateway status`.
3. In the app, set **Gateway URL** to `ws://127.0.0.1:18789`.
4. Save the gateway token.
5. Click **Save**, then **Connect**.
6. Verify Home, Chat, Sessions, Approvals, and Pairing all load.

### Remote operator setup over SSH

Use this when the gateway runs on another machine.

1. Confirm the remote gateway is healthy on the host.
2. Set **Gateway URL** in the app to the local forwarded URL, usually `ws://127.0.0.1:18789`.
3. In **Topology and Tunnels**, set:
   - SSH host
   - remote host, usually `127.0.0.1`
   - local port
   - remote port
4. Start the tunnel.
5. Save settings.
6. Click **Connect**.

### Chat-first workflow

1. Set **Chat session** in Settings or choose a session in **Sessions**.
2. Connect realtime.
3. Open **Chat**.
4. Refresh if needed.
5. Send messages with the native composer.

### Approval workflow

1. Connect realtime.
2. Open **Approvals**.
3. Review command, cwd, agent, session, and risk label.
4. Choose **Allow once**, **Allow & remember**, or **Deny**.
5. Optionally tune the **Approval Policy** card afterward.

### Pairing workflow

1. Connect realtime.
2. Open **Pairing**.
3. Review the request kind, device id, and request id.
4. Approve trusted devices or nodes; reject the rest.

### Canvas/A2UI workflow

1. Enable **Canvas and A2UI node** in Settings.
2. Save settings.
3. Connect realtime.
4. Open **Canvas**.
5. Click **Connect Canvas** if needed.
6. Use **Refresh A2UI** to reload the advertised surface.

### Device validation workflow

1. Open **Devices**.
2. Click **Refresh devices**.
3. Test the capabilities you care about:
   - screenshot
   - bounded screen recording
   - camera still capture
   - notification test
   - overlay test
   - speech clip save
4. Review capability detail and repair guidance for anything unavailable.

### Diagnostics and support workflow

1. Open **Logs**.
2. Review diagnostics summary and location cards.
3. Copy or reveal app and gateway logs as needed.
4. Use **Copy support summary** for escalation notes.
5. Use **Save support artifact** to preserve a machine-readable support snapshot.

## Storage and credential locations

By default, the Windows companion stores local app data under:

```text
%LOCALAPPDATA%\OpenClaw\WindowsCompanion
```

The app state typically includes:

- `preferences.json`
- `activity-diagnostics.jsonl`
- `activity-history.json`
- `notification-history.json`
- captures directory
- speech clips directory

The app also uses the Windows credential store when available for secrets such as the gateway token and device token.

The gateway's own config, auth, channel state, and provider credentials remain in the normal OpenClaw state on the gateway host.

## Troubleshooting

### The app cannot connect to the gateway

Check:

```powershell
openclaw status
openclaw gateway status
openclaw logs --follow
openclaw doctor
```

Then verify:

- the **Gateway URL** is correct
- the gateway token matches the host
- the gateway is reachable on the selected port
- the SSH tunnel is active if using remote mode

Helpful references:

- https://docs.openclaw.ai/gateway
- https://docs.openclaw.ai/gateway/troubleshooting
- https://docs.openclaw.ai/gateway/remote

### Approvals or pairing stay empty

The Windows app only loads approvals and pairing requests after the realtime WebSocket is connected. If Home still shows a disconnected realtime state, connect first.

### Chat does not load

Verify:

- the app is connected
- the selected **Chat session** exists
- the gateway session list is loading under **Sessions**

If necessary, choose a known session from **Sessions** and try again.

### Canvas stays disconnected

Verify:

- **Enable Canvas and A2UI node** is enabled
- the app has connected realtime
- the gateway side expects and trusts the Windows Canvas node

If the status says the node is disabled, the problem is local configuration rather than gateway transport.

### System speech is unavailable

The Windows app now treats missing speech components or voices as an unavailable capability rather than crashing. Install a Windows voice package, then refresh the Devices page.

### Browser proxy is not ready

The current Windows surface reports browser proxy readiness rather than a complete browsing workflow. Review:

- gateway URL validity
- gateway reachability
- URL safety policy

Use the card's repair guidance and the Settings page to fix configuration drift.

### Screen recording expectations do not match a normal video recorder

The Windows companion captures a bounded folder of PNG frames, not a single encoded video file. Lower expectations accordingly and use it as a proof/debugging tool.

## Related references

- Windows platform guide: https://docs.openclaw.ai/platforms/windows
- Gateway runbook: https://docs.openclaw.ai/gateway
- Gateway configuration: https://docs.openclaw.ai/gateway/configuration
- Gateway authentication: https://docs.openclaw.ai/gateway/authentication
- Gateway pairing: https://docs.openclaw.ai/gateway/pairing
- Remote gateway access: https://docs.openclaw.ai/gateway/remote
- Dashboard access: https://docs.openclaw.ai/web/dashboard
