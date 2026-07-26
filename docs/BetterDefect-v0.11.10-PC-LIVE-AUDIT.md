# BetterDefect v0.11.10 PC live audit

- Game: Slay the Spire 2 v0.107.1
- Result: PASS

## Cases

1. Transformed Double Energy + test enchantment which removes `CardKeyword.Exhaust`.
   - Before normal upgrade: Exhaust absent.
   - After `UpgradeInternal` and transformation normalization: Exhaust remains absent.
2. Transformed Feral + official `TezcatarasEmber`.
   - Before normal upgrade: local cost 0.
   - After `UpgradeInternal` changes transformed cost from 2 to 1: final local cost remains 0.

The temporary test mod was removed after the run. BetterDefect dynamic-odds/transformation state was restored byte-for-byte from backup.
