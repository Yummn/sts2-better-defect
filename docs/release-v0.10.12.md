# BetterDefect v0.10.12

## Changes

- Removed the restored rare card `Amplify / 增幅`; `Signal Boost / 信号增强` already provides the same role in Slay the Spire 2.
- Added the Slay the Spire 1 rare Defect skill `Seek / 搜寻`.
  - Cost: 0
  - Base: choose 1 card from the draw pile and move it to the hand.
  - Upgraded: choose 2 cards instead.
  - Exhausts after use.
- Added the correct Seek portrait and removed the Amplify portrait.
- Purges stale `CARD.BD_AMPLIFY` entries from dynamic odds, disabled-card state, enabled-state records and historical-upgrade records so the removed card cannot consume the shared 35-point budget.
- The restored-card count remains 26.

## Compatibility and validation

- Android/mobile: Slay the Spire 2 v0.103.x
- PC: Slay the Spire 2 v0.107.1
- Both targets compile with zero errors.
- Offline regression audit: 144/144.
- REDMI K80 Pro / Android v0.103.2 live validation:
  - BetterDefect v0.10.12 initialized successfully.
  - 26/26 restored cards were registered and the Defect pool expanded from 88 to 114 cards.
  - Seek appeared with its 0-cost portrait and opened the draw-pile selection grid.
  - The selected card moved from the draw pile to the hand.
  - Seek moved to the exhaust pile.
