# Windows Companion Accent And Custom Theme Plan

## Summary

Add appearance customization in two coding sessions:

1. **Session 1: Accent color selection**
   Add a persisted accent color setting under Settings > Appearance with visual color swatches plus names.

2. **Session 2: Custom color themes**
   Add named custom theme presets with visual theme previews, reusing the accent-color infrastructure.

## Session 1: Accent Color Selection

- Extend preferences with `WindowsAccentColorPreference`.
  - Defaults to `System`.
  - Supported values: `System`, `Blue`, `Teal`, `Green`, `Orange`, `Rose`, `Purple`.
  - Persist as a string in `preferences.json`; invalid or missing values default to `System`.

- Add Settings > Appearance UI.
  - Keep existing Theme dropdown.
  - Add `Accent color` below Theme.
  - Use a visual selector, not a plain word-only dropdown.
  - Each option must show a small filled color circle plus the color name.
  - `System` should show a split or outlined circle indicating Windows/system accent.
  - Selection changes apply immediately.
  - Save persists the selected accent; Refresh reloads and reselects it.

- Apply accent color in the shell.
  - Add app-owned accent brushes, for example `AccentBrush` and `AccentTextBrush`.
  - `System` uses WinUI system accent resources where practical.
  - Named accents use fixed accessible light/dark color values.
  - Apply accent to selected navigation affordances and app-owned primary accents.
  - Do not recolor semantic status brushes: success/caution/critical stay semantic.

- Tests.
  - Preference round-trip test for accent color.
  - Missing/unknown accent fallback tests.
  - Visual selector construction test if practical through existing test seams.
  - Run `dotnet test apps/windows/OpenClaw.Windows.Tests/OpenClaw.Windows.Tests.csproj --filter AppPreferencesStoreTests`.
  - Run `pnpm windows:test`, `pnpm windows:build`, and `git diff --check`.

## Session 2: Custom Color Themes

- Extend preferences with `WindowsColorThemePreference`.
  - Defaults to `Default`.
  - Supported values: `Default`, `Slate`, `Forest`, `Ocean`, `Ember`, `HighContrast`.
  - Persist as a string in `preferences.json`; invalid or missing values default to `Default`.

- Add Settings > Appearance UI.
  - Add `Color theme` below Accent color.
  - Use a visual selector with the theme name plus 3-4 small swatches showing background, card, text/accent, and border tone.
  - Apply immediately on selection change.
  - Save and Refresh behave like Theme and Accent color.

- Refactor theme brush application.
  - Replace the current single-theme brush updater with a palette resolver that accepts:
    - brightness from System/Light/Dark,
    - accent color preference,
    - custom color theme preference.
  - Resolver returns all app-owned brushes in one palette object.
  - Continue using WinUI requested theme for framework controls.
  - Keep semantic status colors stable unless `HighContrast` requires adjusted accessible values.

- Tests.
  - Preference round-trip test for color theme.
  - Missing/unknown color theme fallback tests.
  - Palette resolver tests for light/dark and at least one custom theme.
  - Run `dotnet test apps/windows/OpenClaw.Windows.Tests/OpenClaw.Windows.Tests.csproj --filter AppPreferencesStoreTests`.
  - Run `pnpm windows:test`, `pnpm windows:build`, and `git diff --check`.

## Assumptions

- Accent selection must be visual: color circle plus name.
- Custom theme selection must be visual: theme name plus palette swatches.
- Session 1 uses preset accents, not a free-form color picker.
- Session 2 uses preset themes, not user-authored arbitrary palettes.
- Existing System/Light/Dark remains the brightness mode.
