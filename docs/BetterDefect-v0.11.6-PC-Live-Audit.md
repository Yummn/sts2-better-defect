# BetterDefect v0.11.6 PC live audit

- Date: 2026-07-25
- Game: PC v0.107.1
- Result: PASS

## Regression route

1. Loaded a live combat with the transformed Iteration state enabled.
2. Spawned and automatically played Iteration; `IterationPower` was applied.
3. Put Dazed on top of the draw pile and invoked the real `CardPileCmd.Draw` route.
4. Verified Iteration's extra draws completed and Dazed moved to the exhaust pile only after draw completion.
5. Spawned and automatically played Zap after the Iteration trigger.

The final Zap resolved normally, proving the combat action queue was not locked. The log recorded:

`PASS: Iteration played, Dazed exhausted after draw completion, extra draw finished, and subsequent Zap resolved.`
