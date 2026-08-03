# BetterDefect v0.11.35

## Changes

- Removed the dynamic reward-odds engine from BetterDefect.
- Removed the encyclopedia disable/enable button, disabled-card grey mask and probability text from BetterDefect.
- The 50 transformation points now count transformed cards only.
- Added a dedicated persistent transformation state file: `BetterDefect.CardUpgrades.state.dat`.
- Existing `UpgradedCards` selections migrate automatically from the previous BetterDefect dynamic-odds state.
- Card disabling and adaptive reward odds are now provided by the standalone `DynamicCardOdds` mod. Disabled cards have no point cost or quantity limit.

## Compatibility

- Android v0.103.2
- Android v0.110.1
- PC v0.107.1
- No BaseLib dependency

## Validation

- All three target builds completed with zero compilation errors.
- Compiled BetterDefect assemblies contain the card-transformation UI and no dynamic reward-odds or disable implementation.
- Package manifests and ZIP roots were validated before release.
