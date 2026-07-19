# BetterDefect v0.8.3 Android live audit

- Device: REDMI K80 Pro (`24122RKC7C`)
- Game: v0.103.2
- Loaded mods: 8
- Installed BetterDefect DLL SHA-256 matches the final mobile build.
- Stress sequence: open Defect encyclopedia -> verify controls -> return to main menu -> continue current run -> open master deck.
- Encyclopedia result: disable, historical-version, dynamic-odds and 35-point HUD controls remain visible.
- In-run deck result: no `禁用出率`/`启用出率`, historical-version button, dynamic-odds line or disabled overlay.
- BetterDefect log: no matching fatal/exception entry.
- Save handling: pre-test primary and backup saves were restored and both SHA-256 checks matched.

Evidence:
- `disabled-ui-test/v0.8.3/01_defect_library_controls_present.png`
- `disabled-ui-test/v0.8.3/03_run_deck_fixed_no_disable_button.png`
