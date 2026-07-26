## BetterDefect v0.11.12

This release fixes transformed cards silently reverting to vanilla gameplay values after restarting while the Encyclopedia still reports them as enabled.

### Root cause and fix

- Android installs Harmony patches through a delayed AOT-safe queue.
- Card database initialization or run loading could create vanilla card instances before that queue reached BetterDefect's transformation patches.
- BetterDefect now refreshes canonical models and all loaded card piles when the Android patch queue completes.
- It also reapplies persisted transformations after full run deserialization and player-state synchronization.
- Existing portrait choices, disabled-card choices, dynamic odds and transformation selections remain intact.

### Validation

- PC v0.107.1 live automated regression:
  - Reproduced enabled state with a stale loaded Cold Snap at vanilla `1` cost / `6` damage.
  - Production rehydration scanned the live deck and restored `2` cost / `12` damage.
  - Persisted UI state and gameplay state matched after recovery.
- PC v0.107.1 and Android v0.103.2 compiled separately.
- Dual-platform offline audit: 193/193.

Use the platform-labelled archive. Do not mix the PC and Android DLLs.
