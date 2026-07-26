# BetterDefect v0.11.8

## Iteration Android hand-visual cleanup

- Keeps the v0.11.6 safety rule: the first drawn Status is exhausted only after the complete, possibly nested, `CardPileCmd.Draw` task has finished.
- After the exhaust animation resolves, waits for one Godot `ProcessFrame` so queued card-node deletion and hand-holder release are committed before `Draw` returns.
- Defensively removes only empty hand holders or holders whose card model no longer belongs to the hand. Valid hand-card artwork is not hidden or replaced.
- Prevents the Android-only transparent, unselectable card remnant from blocking cards behind it.

## Validation

- PC target: Slay the Spire 2 v0.107.1.
- Test route: play transformed Iteration, put Dazed on top of the draw pile, draw it, verify Dazed enters Exhaust and Iteration's extra draw completes, then play Zap.
- Immediate visual state after the draw: `handModels=7`, `holders=7`, `active=7`, `ghostHolders=0`, `dazedNodes=0`.
- Settled visual state after two seconds: the same 7/7/7 counts with zero ghost holders and zero Dazed nodes.
- Subsequent Zap resolved and the live test reported PASS.
- Offline source/binary audit: 178/178 checks passed.

## Builds

- Android v0.103.2: separately compiled against v103 references.
- PC v0.107.1: separately compiled and live-tested.
- BaseLib is not required.
