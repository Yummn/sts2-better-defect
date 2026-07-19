# BetterDefect v0.8.2

## Android v103 startup guard

- The Android build now skips Harmony's `NCard.Model` setter detour. MonoMod/Harmony on the v103 Android ARM64 runtime could crash natively while generating that detour.
- Encyclopedia features still use the concrete `NCardLibraryGrid` refresh hooks: dynamic odds, disabled-card controls/grey mask, card art controls, historical-version controls and the 35-point HUD remain available.
- The PC v107.1 build keeps the setter hook unchanged.
- All v0.8.1 description/effect fixes remain included.

## Verification

- Source/behavior regression audit: **67 passed, 0 failed**.
- PC v107.1 build: **0 errors**; decompiled `Prepare()` returns `true`.
- Android v103 build: **0 errors**; decompiled `Prepare()` returns `false`.
- REDMI K80 Pro / game v0.103.2: BetterDefect v0.8.2 initialized and reached startup Stage 14.
- Live encyclopedia: 114 Defect cards, dynamic odds text, disabled grey state/button, art button, historical-version button and segmented 35-point HUD were visible. Existing saved state loaded as 25 disabled + 9 historical upgrades = 34/35.
- Live “Fission” search showed only the normal remove-orbs description, with no simultaneous evoke text or duplicated Exhaust line.

## Assets

- `BetterDefect-v0.8.2.zip`: Android v103.
- `BetterDefect-v0.8.2-PC-v107.1.zip`: PC v107.1.
- Archives contain only file entries with `/` separators and no directory entries, for Android settings importer compatibility.
