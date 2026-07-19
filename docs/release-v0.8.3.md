# BetterDefect v0.8.3

This release fixes BetterDefect's encyclopedia-only controls appearing in the in-run master-deck view.

## Fixes

- The shared `NCardLibraryGrid` row-assignment hook now verifies that the grid belongs to a visible `NCardLibrary` before adding controls.
- The generic `NGridCardHolder.SetIsPreviewingUpgrade` refresh now performs the same scope check, preventing delayed callbacks from re-adding controls after cleanup.
- Non-library card views remove BetterDefect's disable/enable button, historical-version button, dynamic-odds text and disabled-card grey mask.
- The encyclopedia keeps its disable state, historical-version switches, dynamic odds, card-art controls and segmented 35-point HUD.

## Verification

- PC v107.1 build: 0 errors.
- Android v0.103.2 build: 0 errors.
- Offline regression suite: 69/69 passed for both binaries.
- REDMI K80 Pro live test: opened the Defect encyclopedia first, then continued the current run and opened the master deck. Encyclopedia controls remained present; the in-run deck contained no BetterDefect controls.
- Test saves were restored byte-for-byte after verification.

## Assets

- `BetterDefect-v0.8.3.zip`: Android/mobile v103.
- `BetterDefect-v0.8.3-PC-v107.1.zip`: PC v107.1.
