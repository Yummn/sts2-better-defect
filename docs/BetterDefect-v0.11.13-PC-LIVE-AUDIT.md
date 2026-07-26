# BetterDefect v0.11.13 PC live audit

- Game: Slay the Spire 2 PC v0.107.1
- Setup: one transformed Loop stack and exactly one Frost orb.
- Observed orb count: `1`
- Observed production `OrbCmd.Passive` calls for that same orb: `2`
- First call source: transformed Loop left-edge path.
- Second call source: transformed Loop right-edge path.
- Result: PASS

Relevant `godot.log` markers:

- `OBSERVE amount=1 orbs=1 watchedPassiveCalls=2`
- `PASS: the sole orb triggered once as the left edge and once as the right edge.`

The original save and BetterDefect settings were restored after the test, and all temporary test mods were removed.
