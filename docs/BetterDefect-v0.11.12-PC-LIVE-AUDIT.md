# BetterDefect v0.11.12 PC live audit

- Game: Slay the Spire 2 PC v0.107.1
- Test: enable transformed Cold Snap, keep a pre-refresh loaded instance at vanilla values, place it in the live player deck, then run the same production rehydration method used after Android patch completion and run loading.
- Before recovery: enabled=`True`, cost=`1`, damage=`6`
- After recovery: cost=`2`, damage=`12`, refreshed cards=`1`
- Result: PASS

Relevant `godot.log` markers:

- `BEFORE cost=1 damage=6 enabled=True`
- `reapplied persisted card transformations to 1 loaded cards (live restart regression)`
- `AFTER cost=2 damage=12 refreshed=1`
- `PASS: persisted enabled state and actual loaded-card transformation match after deserialization.`

The temporary test runner restored the original save and BetterDefect settings after the assertion and removed its test-only mods.
