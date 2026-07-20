# BetterDefect v0.8.8 Android live audit

Tested on 2026-07-20 with a REDMI K80 Pro running Slay the Spire 2 v0.103.2.

## Installed binary

- Manifest version: `0.8.8`
- Private mod path: `/data/data/com.megacrit.stsx/files/mods/BetterDefect`
- DLL SHA-256: `4f25ed623b3afe9ee453a851db299dcf74d64d18c87536e227c66e8d6ef23e88`
- Installed file count: 30

## Live regression path

1. Started the game with BetterDefect and the normal enabled mod set.
2. Opened `百科大全` → card library → Defect cards.
3. Confirmed BetterDefect's disable/version controls and 35-point HUD were visible on the library grid.
4. Opened a card detail popup.
5. Confirmed BetterDefect controls, grey-mask state and HUD disappeared immediately from the dimmed background and were absent from the enlarged card.
6. Returned to the library grid and confirmed the BetterDefect controls/HUD were restored.
7. Left the card library for the compendium landing screen and then the main menu; no BetterDefect control or HUD remained.

CardBeautify's independent `卡图: ...` selectors remained visible behind the card detail, as expected; they are not BetterDefect disable/version controls.

## Runtime evidence

`godot.log` recorded:

- `loaded v0.8.8`
- `checked old Defect card model injection: attempted=26, injected=26`
- `restored 26 old Defect cards to the Defect card pool`
- `Defect GenerateAllCards expanded 88 -> 114`
- `card-version baselines applied; upgrades=9, points=34/35`

No BetterDefect exception, `TypeLoadException`, fatal signal or process exit occurred during the tested route.
