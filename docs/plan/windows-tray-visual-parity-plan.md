# Windows Tray Visual Parity Plan

## Current State

- The Windows companion tray flyout is implemented on `codex/native-windows-foundation-shell`: a NotifyIcon bridge (`WindowsTrayHost`) plus a light-dismiss WinUI flyout driven by a pure snapshot model (`WindowsTraySnapshot` → `TrayFlyoutComposer` → `TrayFlyoutModel` → `TrayFlyoutWindow`).
- The flyout renders status rows (Gateway, Canvas, Sessions, Activity), quick-action rows, inlined permission toggles, support entry points, and gateway lifecycle actions. It is DPI-aware, scrolls when tall, and applies themed hover affordances.
- The `openclaw-windows-node` reference flyout (`E:\Projects\openclaw-windows-node\docs\images\openclawwindows1.png`) is the visual target.

## Scope

This plan covers only the **purely visual / composer-layer** parity gaps with the reference flyout — items with no dependency on gateway data or node/scope work. The data-dependent gaps (per-node topology rows, node-paired/client badge, header master-toggle semantics) are intentionally **excluded** here and are owned by Session 6 of `docs/plan/windows-full-node-scopes-implementation-plan.md`, because they render state that plan produces. The Usage/token row is excluded from both plans until a Windows usage data source exists.

Each item below is achievable in `TrayFlyoutComposer` / `TrayFlyoutModel` / `TrayFlyoutWindow` against the existing snapshot, and is covered by `TrayFlyoutComposerTests` where it affects the model.

## Session 1: Branded Header

Goal: open the flyout with the reference's header band.

Tasks:

- Add a header element to `TrayFlyoutModel` (or a dedicated header record) carrying the app icon, the "OpenClaw" title, and a slot for the master toggle.
- Render the lobster app icon and title in `TrayFlyoutWindow` above the first section.
- Leave the master toggle's binding to the node-scopes plan (Session 6); render a placeholder slot or omit the switch until that lands, rather than wiring it to unrelated state here.

Verification:

- Composer test asserts the header is present in the model.
- VM smoke: header renders, title legible across light/dark/system + accent themes.

## Session 2: Navigable Status Rows

Goal: make status rows entry points like the reference's chevroned rows.

Tasks:

- Extend `TrayStatusRow` with an optional `TrayFlyoutAction` so a row can navigate (Gateway → Connection/Home, Sessions → Sessions page, etc.).
- Render a trailing chevron and route activation through the existing action channel (dismiss-then-navigate) only when a row carries an action.
- Keep display-only rows (no action) non-interactive with no chevron.

Verification:

- Composer tests assert the navigable rows carry the expected actions and display-only rows carry none.
- VM smoke: clicking a status row dismisses the flyout and navigates to the matching page.

## Session 3: Missing Quick Actions

Goal: add the reference's `Quick Send…`, `Reconfigure…`, and `About` actions.

Tasks:

- Add `OpenQuickSend`, `OpenReconfigure`, and `OpenAbout` (names per existing convention) to `TrayFlyoutAction`.
- Add the rows to the quick-actions section in composition order matching the reference.
- Wire `RunTrayAction` in `MainWindow` to the existing shell targets (Quick Send compose surface, the setup/onboarding re-run, and the About surface). Reuse existing navigation rather than adding new pages.

Verification:

- Composer test asserts the three rows exist with the right actions.
- VM smoke: each routes to the expected surface.

## Session 4: Row Affordance Polish

Goal: match the reference's row metadata affordances.

Tasks:

- Render right-aligned accelerator hints on rows that have a keyboard shortcut (e.g. Companion Settings → `Ctrl+Alt+;`), sourced from the existing accelerator definitions.
- Keep the inlined permission toggles as-is; do not adopt the reference's collapsed "Permissions ›" submenu row. The toggles stay directly visible and flippable in the flyout.
- Align labels with the reference where it is clearly better (e.g. "Companion Settings…", "Dashboard") without churning unrelated strings.

Verification:

- Composer tests cover accelerator-hint presence.
- VM smoke: hints render, the inlined toggles still flip and persist without dismissing the flyout.

## Session 5: VM Visual Smoke

Goal: confirm the full visual parity pass holds on a real desktop.

Tasks:

- Side-by-side compare the flyout against `openclawwindows1.png`.
- Verify header, chevrons, new actions, and accelerator hints across light/dark/system themes, accent variations, and 100/125/150/200% DPI.
- Confirm no regressions to the existing crash-safe open/close lifecycle, scrolling, or hover affordances.

Verification:

- Capture a screenshot of the flyout for each theme/DPI combination tested.
