# BetterDefect v0.10.12 Android live audit

- Device: REDMI K80 Pro (`e02b65b6`)
- Game: Slay the Spire 2 v0.103.2
- Package: `com.megacrit.stsx`
- BetterDefect mobile DLL SHA-256: `951696dd560fbe339db1044bce86ccfd947705d29a66332c0cbba135fa942705`

## Startup

- BetterDefect reported v0.10.12.
- Restored-card registration completed: 26/26.
- Defect pool expanded from 88 to 114 cards.
- All five injected power icon mappings initialized.

## Seek test

1. Entered a temporary developer-console combat.
2. Added `BD_SEEK` to the hand.
3. Confirmed the card displays as a 0-cost rare skill with the Seek portrait.
4. Played Seek and confirmed the draw-pile selection grid opened.
5. Selected one card.
6. Confirmed the selected card left the draw pile and entered the hand.
7. Confirmed Seek entered the exhaust pile.

The pre-test run save was restored from the game's own backup after validation.
