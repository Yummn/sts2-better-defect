# BetterDefect v0.11.37

## Changes

- Changed transformed Uproar to deal 5 damage twice before and after its normal upgrade.
- It now randomly auto-plays one current highest-energy-cost card from the draw pile.
- Upgraded transformed Uproar auto-plays two distinct cards, recalculating the draw pile and highest cost after the first card resolves.
- Attack, Skill and Power cards are all eligible; Unplayable cards are excluded.
- If several cards share the highest current cost, the selected card is randomized with the run's shuffle RNG.
- Fixed costs use all current combat modifiers. X-cost cards are ranked as the player's current available energy.
- Updated the card face and Encyclopedia transformation summary to match the new behavior.

## Compatibility

- Android v0.103.2
- Android v0.110.1
- PC v0.107.1
- No BaseLib dependency

## Validation

- All three target builds completed with zero compilation errors.
- The Android v103 bridge binary contains no PC-only `ICombatState` metadata.
- The focused source and binary audit passes 37/37 checks.
