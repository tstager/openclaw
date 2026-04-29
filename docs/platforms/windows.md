---
summary: "Windows support: native app, native CLI and Gateway, and WSL2 install paths"
read_when:
  - Installing OpenClaw on Windows
  - Choosing between native Windows and WSL2
  - Looking for Windows companion app status
title: "Windows"
---

OpenClaw supports both **native Windows** and **WSL2**. WSL2 is the more stable
path for Unix-like tooling. Native Windows supports the CLI, Gateway, and the
OpenClaw Windows companion app.

## WSL2 (recommended)

- [Getting Started](/start/getting-started) (use inside WSL)
- [Install & updates](/install/updating)
- Official WSL2 guide (Microsoft): [https://learn.microsoft.com/windows/wsl/install](https://learn.microsoft.com/windows/wsl/install)

## Native Windows status

Native Windows CLI and app flows are supported. WSL2 remains useful when your
workflow depends on Linux tools or Linux-only plugin dependencies.

What works well on native Windows today:

- website installer via `install.ps1`
- local CLI use such as `openclaw --version`, `openclaw doctor`, and `openclaw plugins list --json`
- native Windows companion app for chat, Gateway controls, pairing, approvals,
  and device capabilities
- embedded local-agent/provider smoke such as:

```powershell
openclaw agent --local --agent main --thinking low -m "Reply with exactly WINDOWS-HATCH-OK."
```

Current caveats:

- `openclaw onboard --non-interactive` still expects a reachable local gateway unless you pass `--skip-health`
- `openclaw onboard --non-interactive --install-daemon` and `openclaw gateway install` try Windows Scheduled Tasks first
- if Scheduled Task creation is denied, OpenClaw falls back to a per-user Startup-folder login item and starts the gateway immediately
- if `schtasks` itself wedges or stops responding, OpenClaw now aborts that path quickly and falls back instead of hanging forever
- Scheduled Tasks are still preferred when available because they provide better supervisor status

If you want the native CLI only, without gateway service install, use one of these:

```powershell
openclaw onboard --non-interactive --skip-health
openclaw gateway run
```

If you do want managed startup on native Windows:

```powershell
openclaw gateway install
openclaw gateway status --json
```

If Scheduled Task creation is blocked, the fallback service mode still
auto-starts after login through the current user's Startup folder.

## Gateway

- [Gateway runbook](/gateway)
- [Configuration](/gateway/configuration)

## Gateway service install (CLI)

Inside WSL2:

```bash
openclaw onboard --install-daemon
```

Or:

```bash
openclaw gateway install
```

Or:

```bash
openclaw configure
```

Select **Gateway service** when prompted.

Repair/migrate:

```bash
openclaw doctor
```

## Windows companion app

The Windows companion app lives in `apps/windows` and is built with WinUI 3,
Windows App SDK, and C#. It uses the same Gateway protocol as the other native
apps and does not own Gateway configuration.

Install the source dependencies once:

```powershell
pnpm install
```

Build and test the app:

```powershell
pnpm windows:protocol:check
pnpm windows:test
pnpm windows:build
```

Launch from source:

```powershell
dotnet run --project apps/windows/OpenClaw.Windows/OpenClaw.Windows.csproj -c Release
```

For local source launches on Windows, prefer the explicit x64 runtime:

```powershell
dotnet run --project apps/windows/OpenClaw.Windows/OpenClaw.Windows.csproj -c Release -r win-x64
```

Package MSIX artifacts:

```powershell
pnpm windows:package
```

The package output is written under `apps/windows/OpenClaw.Windows/AppPackages`.
CI uploads the same directory as the `openclaw-windows-app` artifact from the
Windows app check. The default package command creates unsigned sideload
artifacts for build verification; release builds must sign the MSIX with a
trusted certificate before installing on a clean Windows machine.

After launch, the app connects to the local Gateway URL, usually
`ws://127.0.0.1:18789`. Use the Gateway tab to install, start, stop, restart,
or inspect the native Windows Gateway service. Use the Pairing tab to approve a
pending device pairing request. Use the Devices tab to verify screen capture,
camera, microphone, hotkey, notification, and overlay capabilities.

Uninstall through **Settings > Apps > Installed apps > OpenClaw**, or from
PowerShell:

```powershell
Get-AppxPackage OpenClaw.Windows | Remove-AppxPackage
```

Uninstall removes app-owned package state. Gateway configuration and credentials
remain in the normal OpenClaw user state so reinstalling the app does not erase
the user's existing Gateway setup.

## Gateway auto-start before Windows login

For headless setups, ensure the full boot chain runs even when no one logs into
Windows.

### 1. Keep user services running without login

Inside WSL:

```bash
sudo loginctl enable-linger "$(whoami)"
```

### 2. Install the OpenClaw gateway user service

Inside WSL:

```bash
openclaw gateway install
```

### 3. Start WSL automatically at Windows boot

In PowerShell as Administrator:

```powershell
schtasks /create /tn "WSL Boot" /tr "wsl.exe -d Ubuntu --exec /bin/true" /sc onstart /ru SYSTEM
```

Replace `Ubuntu` with your distro name from:

```powershell
wsl --list --verbose
```

### Verify startup chain

After a reboot before Windows sign-in, check from WSL:

```bash
systemctl --user is-enabled openclaw-gateway.service
systemctl --user status openclaw-gateway.service --no-pager
```

## Advanced: expose WSL services over LAN (portproxy)

WSL has its own virtual network. If another machine needs to reach a service
running **inside WSL** (SSH, a local TTS server, or the Gateway), you must
forward a Windows port to the current WSL IP. The WSL IP changes after restarts,
so you may need to refresh the forwarding rule.

Example (PowerShell **as Administrator**):

```powershell
$Distro = "Ubuntu-24.04"
$ListenPort = 2222
$TargetPort = 22

$WslIp = (wsl -d $Distro -- hostname -I).Trim().Split(" ")[0]
if (-not $WslIp) { throw "WSL IP not found." }

netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=$ListenPort `
  connectaddress=$WslIp connectport=$TargetPort
```

Allow the port through Windows Firewall one time:

```powershell
New-NetFirewallRule -DisplayName "WSL SSH $ListenPort" -Direction Inbound `
  -Protocol TCP -LocalPort $ListenPort -Action Allow
```

Refresh the portproxy after WSL restarts:

```powershell
netsh interface portproxy delete v4tov4 listenport=$ListenPort listenaddress=0.0.0.0 | Out-Null
netsh interface portproxy add v4tov4 listenport=$ListenPort listenaddress=0.0.0.0 `
  connectaddress=$WslIp connectport=$TargetPort | Out-Null
```

Notes:

- SSH from another machine targets the **Windows host IP** (example: `ssh user@windows-host -p 2222`).
- Remote nodes must point at a **reachable** Gateway URL (not `127.0.0.1`); use
  `openclaw status --all` to confirm.
- Use `listenaddress=0.0.0.0` for LAN access; `127.0.0.1` keeps it local only.
- If you want this automatic, register a Scheduled Task to run the refresh step
  at login.

## Step-by-step WSL2 install

### 1. Install WSL2 + Ubuntu

Open PowerShell as Administrator:

```powershell
wsl --install
# Or pick a distro explicitly:
wsl --list --online
wsl --install -d Ubuntu-24.04
```

Reboot if Windows asks.

### 2. Enable systemd (required for gateway install)

In your WSL terminal:

```bash
sudo tee /etc/wsl.conf >/dev/null <<'EOF'
[boot]
systemd=true
EOF
```

Then from PowerShell:

```powershell
wsl --shutdown
```

Re-open Ubuntu, then verify:

```bash
systemctl --user status
```

### 3. Install OpenClaw (inside WSL)

For a normal first-time setup inside WSL, follow the Linux Getting Started flow:

```bash
git clone https://github.com/openclaw/openclaw.git
cd openclaw
pnpm install
pnpm build
pnpm ui:build
pnpm openclaw onboard --install-daemon
```

If you are developing from source instead of doing first-time onboarding, use
the source dev loop from [Setup](/start/setup):

```bash
pnpm install
# First run only (or after resetting local OpenClaw config/workspace)
pnpm openclaw setup
pnpm gateway:watch
```

Full guide: [Getting Started](/start/getting-started)

## Git and GitHub connectivity (contributors)

Some networks block or throttle HTTPS to GitHub. If `git clone` fails with timeouts
or connection resets, try another network, a VPN, or an HTTP/HTTPS proxy your
organization provides.

If `gh auth login` fails during the browser device flow (for example a timeout
reaching `github.com:443`), authenticate with a personal access token instead:

1. Create a token with at least the `repo` scope (classic PAT) or equivalent
   fine-grained access.
2. In PowerShell for the current session:

```powershell
$env:GH_TOKEN="<your-token>"
gh auth status
gh auth setup-git
```

3. If `gh auth status` warns about missing `read:org`, mint a token that includes
   that scope and re-assign the variable:

```powershell
$env:GH_TOKEN="<your-token-with-repo-and-read:org>"
gh auth status
```

`gh auth refresh -s read:org` only applies when you authenticated via `gh auth login`
and have stored credentials to refresh (not when using `GH_TOKEN`).

Never commit tokens or paste them into issues or pull requests.

## Related

- [Install overview](/install)
- [Platforms](/platforms)
