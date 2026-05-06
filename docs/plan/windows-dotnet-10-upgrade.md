# .NET 10 Upgrade Plan For The Windows App

## Summary

Upgrade the native Windows companion app from .NET 8 to .NET 10 in 7 coding sessions. The upgrade is direct, not multi-targeted: `net10.0` for the protocol library and `net10.0-windows10.0.19041.0` for WinUI, WinForms interop, and tests. Keep Windows App SDK on the current stable package already in the repo, `Microsoft.WindowsAppSDK` `1.8.260416003`, unless validation proves a newer stable patch is required.

## Coding Sessions

1. SDK and project baseline
   - Add a repo-scoped SDK pin for .NET 10 so `pnpm windows:*` uses .NET 10 consistently from repo root.
   - Update all Windows app project TFMs: protocol to `net10.0`; app, native interop, and tests to `net10.0-windows10.0.19041.0`.
   - Keep `TargetPlatformMinVersion` at `10.0.19041.0`.
   - Run `pnpm windows:protocol:check`, `pnpm windows:build`, and `pnpm windows:test`.

2. Test host modernization
   - Update the Windows test project for .NET 10 and WinUI expectations: add Windows runtime identifiers and `WindowsAppSdkBootstrapInitialize`.
   - Keep MSTest v4 and update only if `dotnet restore` reports a concrete incompatibility.
   - Add or adjust one test that proves the test host can load Windows App SDK-dependent app code without a bootstrap failure.
   - Run `pnpm windows:test`.

3. Build script and package alignment
   - Update `windows:build`, `windows:test`, and `windows:package` only if .NET 10 exposes RID, restore, or publish warnings that need explicit properties.
   - Preserve unsigned MSIX packaging for local and VM testing.
   - Ensure generated package output remains under ignored `AppPackages` and is not committed.
   - Run `pnpm windows:build` and `pnpm windows:package`.

4. API compatibility cleanup
   - Build with .NET 10 analyzers enabled by default and fix real C# or Windows API compatibility warnings in app, protocol, native interop, and tests.
   - Do not add compatibility shims for .NET 8.
   - Keep WinForms usage isolated to `OpenClaw.Windows.Native`.
   - Run `pnpm windows:build` and `pnpm windows:test`.

5. Protocol generation validation
   - Regenerate C# protocol models only if `pnpm windows:protocol:check` fails after the TFM move.
   - If regeneration changes tracked protocol output, verify the generated code still compiles under `net10.0`.
   - Run `pnpm windows:protocol:check`, then `pnpm windows:build`.

6. VM install smoke
   - Build the unsigned MSIX with .NET 10.
   - Install and run on the Windows 11 VM with the Windows App SDK runtime available.
   - Smoke test launch, tray, gateway connect, settings save, logs, notifications, and app shutdown.
   - Record any VM-only runtime dependency or packaging issue as a code or docs fix before the final session.

7. Docs and final gate
   - Update Windows install/development docs with .NET 10 SDK requirement and the Windows App SDK runtime requirement for VM testing.
   - Run final gates: `pnpm windows:protocol:check`, `pnpm windows:build`, `pnpm windows:test`, `pnpm windows:package`, and `git diff --check`.
   - Commit only the .NET 10 migration files and any required doc updates.

## Public Interfaces And Compatibility

- Public project target frameworks change to:
  - `OpenClaw.Protocol`: `net10.0`
  - `OpenClaw.Windows`: `net10.0-windows10.0.19041.0`
  - `OpenClaw.Windows.Native`: `net10.0-windows10.0.19041.0`
  - `OpenClaw.Windows.Tests`: `net10.0-windows10.0.19041.0`
- Minimum Windows version remains Windows 10 build `19041`.
- No runtime behavior changes are intended beyond moving the app and tests to .NET 10.
- No .NET 8 fallback path should be added.

## Test Plan

- Per-session gates use the existing repo scripts: `pnpm windows:protocol:check`, `pnpm windows:build`, `pnpm windows:test`, and `pnpm windows:package`.
- VM acceptance requires the app to launch, connect to the gateway, exercise tray and menu actions, save settings, show logs, send a test notification, and exit cleanly.
- Final acceptance requires all Windows scripts to pass and no tracked generated package artifacts.

## Assumptions

- Use the installed .NET 10 SDK already present on this machine.
- Keep Windows App SDK `1.8.260416003` unless restore, build, or package output proves it must move.
- Keep the app single-targeted on .NET 10; do not multi-target .NET 8 and .NET 10.
- Treat `.github/skills/` as unrelated unless separately requested.
