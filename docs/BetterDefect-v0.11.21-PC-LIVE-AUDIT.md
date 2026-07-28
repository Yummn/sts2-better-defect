# BetterDefect v0.11.21 PC Live Audit

- Date: 2026-07-28 (Asia/Shanghai)
- Game: Slay the Spire 2 PC v107.1
- Target: transformed Recursion / 递归改造版

## Setup

The combat orb queue was reset to three different orb types in queue order:

1. LightningOrb (visual rightmost)
2. FrostOrb (middle)
3. DarkOrb (visual leftmost)

The real `BD_RECURSION` card was then auto-played with its transformation enabled. Harmony instrumentation counted `OrbCmd.Evoke` calls by orb object identity.

## Result

```text
OBSERVE queue=[LightningOrb,FrostOrb,DarkOrb] right=0 middle=0 left=2
PASS: transformed Recursion double-evoked only the visual leftmost Dark orb and re-channeled Dark.
```

Assertions passed:

- visual rightmost Lightning orb: 0 evokes
- middle Frost orb: 0 evokes
- visual leftmost Dark orb: 2 evokes
- resulting queue remains Lightning, Frost, Dark
- final Dark orb is a newly channeled object of the same type

Temporary test mods were removed after the run, and the pre-test save plus BetterDefect dynamic-odds data were restored.
