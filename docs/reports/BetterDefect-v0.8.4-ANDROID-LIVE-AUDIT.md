# BetterDefect v0.8.4 Android live audit

- Device: REDMI K80 Pro (`24122RKC7C`)
- Game: Android v0.103.2
- Installed DLL SHA-256: `679267A51CC9598B7CD12DD8E5D8CE1930356CEFD9E9BEC47A1B6D01692873D1`
- The installed DLL exactly matches the final mobile package and both install-ready folders.

## Verified sequence

1. Reached the main menu with all currently enabled mods; no BetterDefect point HUD was visible.
2. Opened the compendium landing page; the HUD remained hidden.
3. Opened the card library; the 35-point segmented HUD appeared immediately and showed the saved `33/35` state.
4. Returned to the compendium landing page; the HUD disappeared immediately.
5. Re-entered the card library; the HUD appeared again without requiring a restart, filter action, or card toggle.
6. Returned to the main menu; the HUD remained hidden.

The final successful launch log contains the `loaded v0.8.4` and `card-point HUD visible` markers and no BetterDefect/Harmony exception. The game was force-stopped after verification.
