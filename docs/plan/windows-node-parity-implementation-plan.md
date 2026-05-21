# Windows Companion Parity Implementation Plan

Source documents:

- `docs/plan/windows-node-feature-gap-list.md`
- `docs/plan/windows-node-shared-feature-comparison.md`
- `docs/plan/macos-windows-companion-comparison.md`
- `docs/plan/macos-external-windows-comparison.md`

## Goal

Move `apps/windows` toward the highest-value parity shared by **both** comparison apps:

- **macOS parity** where it improves the Windows companion as a better orchestrator/product surface,
- **external Windows parity** where it improves Windows-native runtime depth,
- without turning `apps/windows` into either a Swift/macOS clone or a second `openclaw-windows-node` rewrite.

The target is a stronger **tray-first, single-shell WinUI 3 companion app** with deeper topology, policy, diagnostics, onboarding, and Windows-native capability coverage.

## What should be extended, not rewritten

The current `apps/windows` app already has the right base shape and should be **extended**:

- tray shell and single-instance lifetime,
- gateway lifecycle controls,
- native chat and session browsing,
- approvals and pairing queue UX,
- base Canvas/A2UI integration,
- Devices page, overlays, hotkeys, and theme customization.

WinUI 3 reality for this plan:

- prefer adding **services, models, and focused pages/dialogs** over introducing a new multi-window shell,
- use the existing app/window/tray model rather than cloning the external app's full window topology,
- treat anything that requires a large new XAML rendering stack or a separate repo/project family as late work.

## Parity framing

### Shared parity work that advances toward both comparison apps

These are the best near-term investments because they help Windows close gaps against **both** targets:

1. broader settings/service seams,
2. richer connection topology handling,
3. deep links and app-driven entry routing,
4. structured diagnostics plus historical operational state,
5. persistent approval/policy depth,
6. safer capability execution and URL handling,
7. browser/media/node capability expansion,
8. guided onboarding beyond static readiness checks.

### macOS-skewed areas

These matter for product breadth, but should stay **late or explicitly partial** on Windows:

- discovery breadth beyond SSH/direct basics,
- large admin/settings breadth,
- voice wake/talk product features,
- automation bridge breadth like Peekaboo,
- full macOS-style orchestrator behavior around launchd/Tailscale-specific flows.

### external-Windows-skewed areas

These are valuable, but should be treated as **late Windows-specific follow-ons** unless a smaller slice is cheap:

- full native A2UI renderer,
- MCP-only local server mode,
- localization packs beyond a scaffold,
- PowerToys Command Palette integration,
- WinNode CLI utility,
- ElevenLabs and other extra TTS providers.

## Sequencing principles

1. **Do shared parity work first.** Prefer sessions that move Windows closer to both comparison apps before taking on one-sided parity.
2. **Strengthen seams before surfacing more UI.** Settings, topology, diagnostics, and policy infrastructure should land before richer WinUI affordances.
3. **Keep the shell compact.** Favor page/dialog expansion inside the current shell; only add extra windows when history/activity UX clearly benefits.
4. **Land safety before reach.** Persistent exec policy, URL risk evaluation, and redaction should precede browser proxy and richer media flows.
5. **Split app parity from repo parity.** `apps/windows` work should not be blocked by Command Palette, CLI, or MCP-server follow-ons.

## Revised phased session structure

| Revised session | Absorbs from current plan | Primary parity value | Notes |
| --- | --- | --- | --- |
| **1. Platform seams and settings expansion** | Session 1 | **Shared** | Keep first. Enables every later phase. |
| **2. Topology, deep links, and guided entry points** | Session 2 + onboarding-routing slice of Session 8 | **Shared** | Moves toward macOS topology and external Windows deep-link/setup flows. |
| **3. Operational backbone** | Session 3 + port diagnostics parts of Session 2 | **Shared** | Build diagnostics/activity/history substrate before UI polish. |
| **4. Policy and safe execution foundation** | Session 5 | **Shared** | Needed before browser/media expansion. |
| **5. Windows capability parity** | Session 6 + core of Session 7 | **Shared**, with external-Windows depth | Browser proxy, screen recording, and Windows TTS belong together as capability growth. |
| **6. Operational UX and notification rules** | Session 4 + notification-rule slice of Session 8 | **Shared**, leaning external-Windows | Expose history/diagnostics once the backend exists. |
| **7. Guided onboarding and focused admin expansion** | remaining onboarding/admin slice of Session 8 | **Shared**, leaning macOS | Deeper onboarding should come after topology, diagnostics, and capabilities exist. |
| **8. Late Windows-specific parity follow-ons** | app-local slice of Session 9 | **External-Windows-only** | Keep app-local localization or other selective Windows extras here. |
| **9. Stretch repo-level follow-ons** | repo-level slice of Session 9 | **External-Windows-only / stretch** | Command Palette, WinNode CLI, MCP server, full native A2UI renderer. |

## Session details

## Session 1 - Platform seams and settings expansion

**Why first:** this is the smallest safe step that unlocks both comparison paths without committing to a rewrite.

**In scope**

- expand persisted settings beyond appearance/basic shell preferences,
- add service-registration seams for topology, diagnostics, policy, history, browser, and media services,
- add only the navigation placeholders needed for later phases,
- keep breaking up `MainWindow.cs` only where it reduces future coupling.

**Parity target:** **shared parity work** for both comparison apps.

## Session 2 - Topology, deep links, and guided entry points

**Why here:** both comparison apps are ahead on how users enter and route through the product, but the enabling work is mostly shared infrastructure.

**In scope**

- SSH tunnel settings and lifecycle management,
- `openclaw://` registration plus second-instance handoff,
- gateway/tunnel/browser-proxy port/topology model,
- port diagnostics plumbing needed by setup and support flows,
- minimal guided connection entry surfaces that can later grow into richer onboarding.

**Parity target:** **shared parity work**.

**WinUI 3 note:** prefer routing into existing shell pages/dialogs instead of building a separate window graph.

## Session 3 - Operational backbone

**Why before more UX:** the app needs durable operational state before richer history surfaces make sense.

**In scope**

- structured JSONL diagnostics with bounded async writing and rotation,
- activity stream service,
- notification history storage,
- structured event emission from gateway/device/tunnel flows.

**Parity target:** **shared parity work**.

This session closes gaps against external Windows operational tooling while also helping Windows catch up with macOS's broader runtime coordination.

## Session 4 - Policy and safe execution foundation

**Why before capability expansion:** browser/media/node growth should not bypass local safety rails.

**In scope**

- persistent exec approval policy,
- clearer one-time vs stored approval behavior,
- URL risk evaluation,
- A2UI secret redaction,
- policy storage seams that later settings/onboarding flows can reuse.

**Parity target:** **shared parity work**.

This is the clearest session where Windows can close **macOS policy depth** and **external Windows persistent-rule depth** at the same time.

## Session 5 - Windows capability parity

**Why here:** once topology and policy exist, Windows can add the highest-value native/runtime capabilities without unstable layering.

**In scope**

- browser proxy capability,
- screen recording with bounded duration/fps and realistic WinUI/Windows capture limits,
- Windows system TTS provider,
- status/repair guidance inside the current shell.

**Parity target:** mostly **shared parity work**, with stronger pull from the external Windows comparison.

**Recommended split if needed**

- **5A:** browser proxy,
- **5B:** screen recording + Windows TTS.

**Explicitly not in this session**

- voice wake/talk mode,
- automation bridge breadth,
- ElevenLabs,
- screen-recording audio capture unless the implementation stays very small.

## Session 6 - Operational UX and notification rules

**Why after Session 3:** build the UX only after the backing services are proven.

**In scope**

- activity stream page/window with filtering and copy/clear,
- notification history page/window with timestamps and deep links,
- support-bundle/copy-summary actions,
- stored notification categorization rules with a minimal editor.

**Parity target:** **shared parity work**, leaning toward external Windows operational depth.

**WinUI 3 note:** prefer one focused history surface at a time; avoid multiplying secondary windows unless usability clearly improves.

## Session 7 - Guided onboarding and focused admin expansion

**Why late-middle:** richer onboarding should be built on real topology, diagnostics, policy, and capability checks rather than mocked placeholders.

**In scope**

- evolve onboarding from diagnostics-only checks into guided connection/setup flows,
- expose the new topology/policy/capability settings coherently,
- add only the highest-value admin/settings depth needed to support the new runtime features.

**Parity target:** **shared parity work**, but mainly where Windows should borrow from macOS breadth without inheriting macOS-only platform assumptions.

**Explicitly partial**

- do not attempt full macOS settings breadth,
- do not attempt discovery/Tailscale parity in this phase.

## Session 8 - Late Windows-specific parity follow-ons

**Why late:** these are useful, but not required to claim a much stronger `apps/windows` companion.

**Reasonable candidates**

- MRT/resource-based localization scaffold,
- one proof locale after the scaffold is stable,
- selective app-local Windows affordances that do not require a new repo-scale architecture.

**Parity target:** **external-Windows-only**.

**Callout:** localization is correctly late because the current Windows shell is heavily code-built and would need broad string extraction.

## Session 9 - Stretch repo-level follow-ons

**Why separate:** these are not core `apps/windows` parity work and should not distort the app roadmap.

**Candidates**

- PowerToys Command Palette extension,
- WinNode CLI utility,
- MCP-only local server mode,
- full native A2UI renderer parity.

**Parity target:** **external-Windows-only / stretch**.

**Callout:** full native A2UI renderer should stay here; it is a large WinUI/XAML hosting investment, not a small extension of the current shell.

## Current-plan changes to make

### Merge

- **Merge current Sessions 3 and 4 conceptually** into one diagnostics track: backend first, UX immediately after.
- **Merge current Sessions 6 and 7** under a single capability-parity phase, optionally split internally into 5A/5B.

### Rename

- Session 1 → **Platform seams and settings expansion**
- Session 2 → **Topology, deep links, and guided entry points**
- Session 5 → **Policy and safe execution foundation**

### Reorder

- move onboarding work earlier in concept, but only as **entry routing/topology-aware setup** in Session 2,
- move full onboarding depth later to Session 7,
- keep policy ahead of browser/media work,
- move localization out of the same bucket as repo-level extras.

### Split

- **Current Session 8** should split into:
  - early entry/topology work,
  - late onboarding/admin depth,
  - notification rules aligned with operational UX.
- **Current Session 9** should split into:
  - app-local late parity,
  - repo-level stretch work.

## Out-of-plan or explicit stretch scope

These should remain out of the main parity plan or be called late/stretch:

- voice wake/talk product parity,
- Peekaboo-style automation/admin breadth,
- broad discovery and Tailscale-like topology parity,
- full native A2UI renderer parity,
- MCP-only local server mode,
- PowerToys Command Palette,
- WinNode CLI,
- ElevenLabs provider,
- screen-recording audio capture beyond a trivial add-on.

## Milestones

### "Shared parity core" milestone

Sessions **1 through 5**.

This is the best stopping point for a realistic Windows upgrade that materially improves parity with **both** comparison apps.

### "Operational parity" milestone

Add **Session 6**.

### "Guided product parity" milestone

Add **Session 7**.

### "Late Windows-specific parity" milestone

Add **Sessions 8 and 9** only if the core app roadmap is already healthy.

## Immediate recommendation

Start with:

1. **Session 1 - Platform seams and settings expansion**
2. **Session 2 - Topology, deep links, and guided entry points**
3. **Session 3 - Operational backbone**
4. **Session 4 - Policy and safe execution foundation**

That order keeps the roadmap realistic for `apps/windows` and WinUI 3: it strengthens the existing shell, avoids a giant rewrite, and front-loads the work that advances Windows toward **both** comparison apps before taking on Windows-only stretch features.
