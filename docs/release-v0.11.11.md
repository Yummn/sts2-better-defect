## BetterDefect v0.11.11

This release fixes transformed Chaos being unable to generate Glass.

### Fix

- `ModelDb.Orbs` contains only Lightning, Frost, Dark and Plasma, while the vanilla random-orb pool also contains Glass.
- Transformed Chaos now constructs the complete five-orb gameplay pool explicitly.
- Its existing missing-type priority is preserved, so Glass is selected when it is the only absent orb type.
- Chinese card text and the Encyclopedia transformation summary now explicitly include Glass.

### Validation

- PC v0.107.1 live automated test:
  - The exact production selector returned `GlassOrb` when Lightning, Frost, Dark and Plasma were marked occupied.
  - That selected model was channeled through the game's real `OrbCmd.Channel` path.
  - The live queue became `[LightningOrb, GlassOrb]`.
- PC v0.107.1 and Android v0.103.2 compiled separately.
- Dual-platform offline audit: 187/187.

Use the platform-labelled archive. Do not mix the PC and Android DLLs.
