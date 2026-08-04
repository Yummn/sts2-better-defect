# BetterDefect v0.11.38

## Changes

- Fixed Fuel created by transformed Compact failing to play on Android v0.110.1.
- Fuel no longer indexes the missing `Cards` dynamic variable in the v0.110.1 game model.
- Draw count is resolved directly from Compact's transformation state and Fuel's normal upgrade level.
- Base Fuel gains 1 Energy and draws 1 card; Fuel+ gains 1 Energy and draws 2 cards.
- Updated the Chinese Fuel text to use the native upgrade selector and match the actual effect.

## Compatibility

- Android v0.103.2
- Android v0.110.1
- PC v0.107.1
- No BaseLib dependency

## Validation

- All three target builds completed with zero compilation errors.
- The focused Fuel source/binary audit passes 12/12 checks, and the transformed Uproar regression audit passes 37/37 checks.
- Android v0.110.1 live validation confirmed Compact+ transformed three Status cards into Fuel+.
- Fuel+ displayed `获得1能量，抽2张牌`, played successfully, raised Energy from 3 to 4 and drew two cards.
- The live log contains neither `KeyNotFoundException: Cards` nor the SmartFormat missing-`Cards` selector error.
- Android startup loaded v0.11.38 and installed all 35/35 scheduled patch classes.
