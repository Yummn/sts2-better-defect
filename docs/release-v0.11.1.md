# BetterDefect v0.11.1

## Compatibility

- Android: Slay the Spire 2 v0.103.x
- PC: Slay the Spire 2 v0.107.1
- BaseLib is not required

## Changes

- Fixes intermittent Android ARM64 Harmony/MonoMod startup crashes introduced by the new v0.11.0 rare-card transformations.
- Splits the seven rare-card `OnPlay` routes into independently diagnosable Harmony patches.
- Omits the redundant Android `CardPoolModel.GetUnlockedCards` hook because the patched Defect `GenerateAllCards` route already supplies the restored cards.
- Omits two Android-only duplicate tooltip detours that do not affect card descriptions or combat behavior.
- Preserves every v0.11.0 transformation, rarity migration, dynamic odds value, disabled state, card-art selection and 35-point transformation budget.

## Verification

- PC v0.107.1 build: succeeded.
- Android v0.103.2 build: succeeded.
- Offline behavior audit: 166/166 passed.
- REDMI K80 Pro live test: the game reached the main menu; BetterDefect v0.11.1 and all seven rare-card patches initialized; no BetterDefect errors or fatal signals were present after a clean device reboot.
