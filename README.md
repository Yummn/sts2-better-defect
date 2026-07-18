# BetterDefect

Slay the Spire 2 BetterDefect mod. It restores 26 old Defect cards and adds cross-run dynamic reward odds, restored old Defect portraits for CardBeautify, card-library disable controls, and the Fission orb visual fix.

Compatibility: mobile v103 and PC v107 series. Download from GitHub Releases; each release asset is an install-ready zip whose `BetterDefect` folder can be copied into `mods/`.

## Latest

- [v0.6.21](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.21): Android-safe follow-up for encyclopedia-only controls. Keeps the no-global-card-refresh-hook performance design, gates the disable button/grey mask/dynamic-odds text to the real encyclopedia `NCardLibraryGrid`, and cleans touched card nodes once when leaving the encyclopedia so in-run/shop deck views do not inherit BetterDefect UI.

## History

- [v0.2.1](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.2.1)
- [v0.3.0](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.3.0)
- [v0.4.0](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.4.0)
- [v0.4.1](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.4.1)
- [v0.5.1-v103-full](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.5.1-v103-full)
- [v0.5.2](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.5.2)
- [v0.5.9](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.5.9)
- [v0.6.3](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.3): restored dynamic odds and card disabling.
- [v0.6.4](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.4): disabled-card grey look and larger mobile touch button.
- [v0.6.5](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.5): disable button uses CardBeautify-style UI.
- [v0.6.6](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.6): added segmented disabled-card counter HUD.
- [v0.6.8](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.8): fixed disabled-card progress bar visibility.
- [v0.6.9](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.9): temporarily removed the in-game disable/enable option and disabled-card counter HUD.
- [v0.6.13](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.13): restored the card-library disable/enable UI on mobile, fixed Chinese dynamic-odds text, restored the disabled-card grey mask, and showed a compact top segmented disabled-card counter HUD.
- [v0.6.14](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.14): kept the disabled-card counter HUD visible only inside the encyclopedia/card-library screen and hid it on the main menu, loading, combat, shop, and other non-library screens. Verified on Android v103 via ADB screenshots.
- [v0.6.15](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.15): limited the disable button, grey disabled mask, and dynamic-odds text strictly to the encyclopedia/card-library screen, and cleaned old UI from reused card nodes outside the library.
- [v0.6.16](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.16): restored the starter-deck tweak that replaces one Defect Strike with Ball Lightning, while keeping the v0.6.15 encyclopedia-only UI cleanup.
- [v0.6.17](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.17): Android performance pass for draw/play stutter. The hot `NCard.UpdateVisuals` path now returns before Defect checks/reflection/text cleanup outside the encyclopedia, and the disabled-card HUD stops full tree scans while hidden.
- [v0.6.18](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.18): strictly gates card controls to the concrete `NCardLibrary` screen and excludes in-run/shop deck and pile inspection screens.
- [v0.6.19](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.19): Android FPS/performance pass; removes the global `NCard.UpdateVisuals` UI hook, makes the UI event-driven from the card library grid, disables HUD polling, and adds small lookup caches.
- [v0.6.21](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.21): keeps the Android-safe no-global-card-hook design, verifies controls against the actual `NCardLibrary` ancestor, and performs one-shot cleanup on card-library close so in-run/shop deck views stay clean.

## v0.6.21 feature summary

- Restores 26 old Defect cards to the Defect card pool and card library.
- Defect starter deck replacement is back: one starting `StrikeDefect` is replaced by `BallLightning`, with a duplicate guard so the patch will not replace multiple Strikes if it runs again.
- Mobile draw/play hot path optimized: outside the encyclopedia/card-library screen, `NCard.UpdateVisuals` no longer runs Defect-card pool checks, reflection-based description cleanup, dynamic-odds text injection, disable button creation, or grey-mask work.
- The disabled-card counter HUD no longer performs periodic full scene-tree scans while it is hidden; entering the encyclopedia still shows it through card-library `NCard.UpdateVisuals` refresh.
- Cross-run dynamic odds for Defect cards only: selected cards gain weight, exactly three-card Defect skip/reroll rewards subtract a total group weight, and each rarity is handled independently.
- Encyclopedia/card-library UI shows `动态出率：x.xx` in Chinese without mojibake.
- In the encyclopedia/card-library only, Defect cards have a mobile-sized `禁用出率` / `启用出率` button; disabled cards are excluded from reward replacement, show `0.00x（已禁用）`, and get a grey aligned mask.
- Non-encyclopedia card views remove BetterDefect's disable button, grey mask, and dynamic-odds text. v0.6.18 also removes the old broad `CardLibrary` namespace/name fallback and explicitly excludes `NCardPileScreen`, deck, draw pile, discard pile, exhaust pile and shop deck views.
- v0.6.21 keeps the v0.6.19 no-global `NCard.UpdateVisuals` design. Encyclopedia controls are updated only by card-library/grid events, with stricter `NCardLibrary` ancestor verification and a one-shot cleanup when the card library closes.
- v0.6.21 keeps HUD polling disabled; the HUD is shown/hidden by card-library events instead.
- The top segmented disabled-card counter shows 0-25 in blue and 26-35 in red, and remains visible only inside the encyclopedia/card-library screen.
- `Data/Portraits/*.png` is included for CardBeautify's restored old Defect art.
- Defect-card type checks and restored old-card list generation are cached to reduce repeated reflection/model lookups on Android.
- No BaseLib dependency.

## Install

Download `BetterDefect-v0.6.21.zip` from Releases, unzip it, and copy the `BetterDefect` folder into the game's `mods` folder.
