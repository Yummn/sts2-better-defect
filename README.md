# BetterDefect

Slay the Spire 2 BetterDefect mod. It restores 26 old Defect cards and adds cross-run dynamic reward odds, restored old Defect portraits for CardBeautify, card-library disable controls, and the Fission orb visual fix.

Compatibility: mobile v103 and PC v107 series. Download from GitHub Releases; each release asset is an install-ready zip whose `BetterDefect` folder can be copied into `mods/`.

## Latest

- [v0.6.14](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.14): keeps the disabled-card counter HUD visible only inside the encyclopedia/card-library screen and hides it on the main menu, loading, combat, shop, and other non-library screens. Verified on Android v103 via ADB screenshots.

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

## v0.6.14 feature summary

- Restores 26 old Defect cards to the Defect card pool and card library.
- Cross-run dynamic odds for Defect cards only: selected cards gain weight, exactly three-card Defect skip/reroll rewards subtract a total group weight, and each rarity is handled independently.
- Card library / large card UI shows `动态出率：x.xx` in Chinese without mojibake.
- Defect cards have a mobile-sized `禁用出率` / `启用出率` button; disabled cards are excluded from reward replacement, show `0.00x（已禁用）`, and get a grey aligned mask.
- The top segmented disabled-card counter shows 0-25 in blue and 26-35 in red, but v0.6.14 now displays it only inside the encyclopedia/card-library screen.
- `Data/Portraits/*.png` is included for CardBeautify's restored old Defect art.
- No BaseLib dependency.

## Install

Download `BetterDefect-v0.6.14.zip` from Releases, unzip it, and copy the `BetterDefect` folder into the game's `mods` folder.
