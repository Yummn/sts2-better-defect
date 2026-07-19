# BetterDefect v0.8.4

This release fixes the segmented 35-point card-upgrade HUD appearing outside the card library and occasionally failing to reappear.

## Fixes

- The HUD is bound to the exact `NCardLibrary` instance that requested it.
- `NSubmenu.OnScreenVisibilityChange` now drives hide/restore behavior in addition to open/close events.
- The old global scene-tree visibility/name scans are removed; only the bound card-library instance is checked.
- Unrelated generic card-grid callbacks can no longer hide a valid library HUD.
- Disabled/upgraded labels refresh when their individual counts change even if their combined point total is unchanged.

## Verification

- PC v107.1 build: 0 errors.
- Android v0.103.2 build: 0 errors.
- Offline regression suite: 72/72 passed for both binaries.
- REDMI K80 Pro live sequence: HUD hidden on main menu and compendium landing page, visible in card library, hidden after leaving, and visible again after re-entry.
- Final installed Android DLL matched the release package by SHA-256.

## Assets

- `BetterDefect-v0.8.4.zip`: Android/mobile v103.
- `BetterDefect-v0.8.4-PC-v107.1.zip`: PC v107.1.
