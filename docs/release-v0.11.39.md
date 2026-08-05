# BetterDefect v0.11.39

## Changes

- Transformed Uproar now auto-plays only Attack cards from the draw pile.
- It still deals 5 damage twice, then randomly chooses 1 Attack, or 2 distinct Attacks when upgraded, from the current highest-energy-cost tier.
- Reworded Uproar's Chinese card text and encyclopedia summary to use native Slay the Spire terminology.
- Removed Loop's obsolete one-orb trigger restriction from its card and power descriptions; one orb correctly counts as both the leftmost and rightmost orb.
- Reworded transformed Synthesis so its normal and upgraded draw/selection behavior is described directly instead of with editorial parentheses.
- Audited all behavior-changing transformation descriptions against their implemented routes.

## Compatibility

- Android v0.103.2
- Android v0.110.1
- PC v0.107.1
- No BaseLib dependency

## Validation

- All three target builds completed with zero compilation errors.
- The focused transformed Uproar source/binary audit passes 37/37 checks.
- The card-description audit passes 9/9 checks and covers every behavior-changing card-face override.
- The transformed Compact/Fuel regression audit passes 12/12 checks.
- The Android v103 binary contains no PC-only `ICombatState` metadata.
