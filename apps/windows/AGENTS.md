# Windows App Guide

This subtree owns the native Windows companion app.

## Scope

- Build with WinUI 3, C#, and Windows App SDK.
- Keep gateway protocol models generated from `scripts/protocol-gen-csharp.ts`.
- Use existing Gateway CLI/service contracts for process and service lifecycle.
- Do not duplicate gateway configuration ownership in the app.

## Commands

- Generate protocol models: `pnpm windows:protocol:gen`
- Check generated protocol models: `pnpm windows:protocol:check`
- Build: `pnpm windows:build`
- Test: `pnpm windows:test`
- Package MSIX artifacts: `pnpm windows:package`

## Boundaries

- App code lives in `OpenClaw.Windows`.
- Windows API adapters live in `OpenClaw.Windows.Native`.
- Generated protocol code lives under `OpenClaw.Protocol/Generated` and must not be edited by hand.
- Tests live in `OpenClaw.Windows.Tests`.
- MSIX output under `OpenClaw.Windows/AppPackages` is generated and must not be committed.
