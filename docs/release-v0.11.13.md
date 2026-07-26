## BetterDefect v0.11.13

This release removes transformed Loop's one-trigger restriction when only one orb is present.

### Change

- A sole orb now counts as both the leftmost and rightmost orb.
- Each transformed Loop stack therefore triggers that same orb twice.
- With two or more orbs, Loop still triggers the leftmost and rightmost orb once each per stack.

### Validation

- PC v0.107.1 live automated test used one transformed Loop stack and exactly one Frost orb.
- The production `OrbCmd.Passive` route was called twice for the same orb: once from the left-edge path and once from the right-edge path.
- PC v0.107.1 and Android v0.103.2 compiled separately.
- Dual-platform offline audit: 194/194.

Use the platform-labelled archive. Do not mix the PC and Android DLLs.
