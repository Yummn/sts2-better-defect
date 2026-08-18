# BetterDefect v0.11.55

## Changed

- Transformed **Hyperbeam** now follows the official v0.111 beta design:
  - Costs 2 Energy.
  - Deals 24 (30) damage to all enemies.
  - Loses 3 Focus for the current turn only.
- Added a dedicated temporary negative-Focus power so repeated applications stack and the exact amount is restored at turn end.
- Updated the card text, transformation summary, status icon mapping, and Android v103/v110 turn-hook compatibility.

## Verification

- Android v110.1 live combat test played the transformed card successfully.
- The card model reported 24 base damage and 3 temporary Focus loss.
- Focus changed `0 -> -3` on play and returned `-3 -> 0` at turn end.
- Android v103, Android v110.1 and PC v107.1 builds completed successfully.
