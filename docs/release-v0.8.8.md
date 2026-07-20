# BetterDefect v0.8.8 — Encyclopedia UI scope fix

This release fixes BetterDefect's encyclopedia-only controls appearing after opening the encyclopedia in card details, combat/shop deck views, and other non-encyclopedia screens.

## Changes

- Bind controls to the exact active `NCardLibraryGrid` owned by the visible encyclopedia.
- Detect card-detail popups hosted under the same library and invalidate the grid scope while they are open.
- Hide buttons/overlays synchronously and disable input before freeing pooled UI nodes.
- Clean touched cards whenever the library watcher observes a detail or exit transition.
- Restore controls and the 35-point HUD after returning to the encyclopedia card list.

## Verification

- PC v0.107.1: list → detail → list regression passed.
- Android v0.103.2 / REDMI K80 Pro: list → detail → list → compendium → main menu regression passed.
- Offline audit: 81/81 checks passed.
- Restored Defect model injection remained 26/26; Defect card library remained 114 cards.

## Assets

- `BetterDefect-v0.8.8-Mobile-v103.zip`: Android v0.103.2 build.
- `BetterDefect-v0.8.8-PC-v107.1.zip`: PC v0.107.1 build.
