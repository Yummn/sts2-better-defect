# BetterDefect v0.11.2

## Compatibility

- Android: Slay the Spire 2 v0.103.x
- PC: Slay the Spire 2 v0.107.1
- BaseLib is not required

## Changes

- Raises the shared disabled-card and card-transformation budget from 35 to 50 points.
- Points 1–25 are blue and represent the **Normal / 正常** stage.
- Points 26–35 are yellow and represent the **Overclock / 超频** stage.
- Points 36–50 are red and represent the **Overload / 过载** stage.
- Adds visible gaps between the three HUD sections.
- The HUD title and counter color now follow the current stage.
- Existing saved disabled-card, transformed-card, dynamic-odds and card-art selections remain compatible.
- Retains the Android ARM64 startup-stability work from v0.11.1.

## Verification

- PC v0.107.1 build: succeeded.
- Android v0.103.2 build: succeeded.
- Dual-binary offline behavior audit: 172/172 passed.
- REDMI K80 Pro: internal and external mod copies match the packaged mobile DLL; v0.11.2 initialized successfully after launching the game activity.
