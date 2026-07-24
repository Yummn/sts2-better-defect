# BetterDefect v0.10.13

## Changes

- Added an optional 35-point-budget transformation for `Coolheaded / 冷静头脑`.
  - Base: costs 1, draws 2 cards, then channels 1 Frost, and Exhausts.
  - Upgraded: keeps the same cost, draw and Frost channel, but removes Exhaust.
- Changed the optional transformation for `Chaos / 混沌`.
  - Base: costs 1, channels 2 random orbs, prioritizes orb types not currently present, and Exhausts.
  - Upgraded: still channels 2 orbs and removes Exhaust.
- Synchronized combat behavior, normal-upgrade handling, encyclopedia summaries and Chinese card descriptions.
- Preserved existing dynamic-odds weights, disabled cards, portrait selections and transformation selections in persistent storage.

## Compatibility and validation

- Android/mobile: Slay the Spire 2 v0.103.x
- PC: Slay the Spire 2 v0.107.1
- Both targets compile with zero errors.
- Dual-target offline regression audit: 151/151.
- REDMI K80 Pro / Android v0.103.2 startup validation:
  - BetterDefect v0.10.13 initialized successfully.
  - The common-card custom-play patch and normal-upgrade patch attached successfully.
  - 26/26 restored cards registered and the Defect card pool expanded from 88 to 114.
  - Existing persistent state loaded successfully: 113 dynamic-odds records and 1 disabled card.
