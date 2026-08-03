# BetterDefect v0.11.36

## Changes

- Added the Defect-only Ancient card `偏差认知*改` for Darv's Dusty Tome.
- Darv no longer chooses vanilla Biased Cognition for the Defect.
- `偏差认知*改` costs 1 and grants 4 Focus, or 5 after its normal upgrade.
- Its power loses 2 Focus at the start of each player turn.
- Every negative Focus change received while the power is active is reduced by 1. This includes the card's own upkeep and temporary-Focus rollback from Hotfix-like effects.
- The card remains Ancient and therefore does not enter ordinary Common, Uncommon or Rare reward rolls.
- Dusty Tome's native `AfterObtained` route upgrades the selected card before adding it to the deck, so Darv always grants `偏差认知*改+`.
- The card reuses Biased Cognition's current portrait, including a CardBeautify replacement when that mod is active.

## Compatibility

- Android v0.103.2
- Android v0.110.1
- PC v0.107.1
- No BaseLib dependency

## Validation

- All three target builds completed with zero compilation errors.
- The Android v103 bridge binary contains no PC-only `ICombatState` metadata.
- The focused source, game-route and binary audit passes 46/46 checks.
