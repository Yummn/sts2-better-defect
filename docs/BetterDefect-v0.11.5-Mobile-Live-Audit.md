# BetterDefect v0.11.5 Android live audit

- Date: 2026-07-24
- Device: REDMI K80 Pro (`e02b65b6`)
- Game: Android v0.103.2
- Result: PASS

## Reproduction before the fix

Playing Ball Lightning failed in `CardModel.OnPlayWrapper` with:

`System.MissingMethodException: Method not found: Task Func<CardModel, PlayerChoiceContext, CardPlay>.Invoke(...)`

The card action completed with an exception and the card remained unresolved.

## Validation after the fix

1. Cold start reached the main menu.
2. BetterDefect reported v0.11.5, registered the reflection-safe Android card-play bridge, and completed the 34/34 deferred patch queue.
3. Ball Lightning played normally: energy changed from 3 to 2, the card left the hand, the enemy changed from 42 HP to 35 HP, and a second Lightning orb appeared.
4. Strike played normally through the native fallback: energy changed from 2 to 1, the card left the hand, and the enemy changed from 35 HP to 29 HP.
5. Neither play produced `MissingMethodException` or `PlayCardAction` exceptions.

Evidence is retained locally under `live-fixed-20260724/`.

