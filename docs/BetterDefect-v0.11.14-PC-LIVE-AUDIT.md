# BetterDefect v0.11.14 PC live audit

- Game: Slay the Spire 2 PC v0.107.1
- Card: transformed Consuming Shadow+
- Base transformed Repeat: `1`
- Upgraded transformed Repeat: `3`
- Dark orbs present after actually playing the upgraded card: `3`
- Result: PASS

Relevant `godot.log` markers:

- `OBSERVE repeat=3 darkOrbs=3`
- `PASS: transformed Consuming Shadow+ generated three Dark orbs.`

The original save and BetterDefect settings were restored after the test, and all temporary test mods were removed.
