# BetterDefect

Slay the Spire 2 BetterDefect mod. It restores 26 old Defect cards and adds cross-run dynamic reward odds, per-card disabling, disabled-card counter HUD, and restored old Defect portraits for CardBeautify.

Compatibility: mobile v103 and PC v107 series. Download from GitHub Releases; each release asset is an install-ready zip whose `BetterDefect` folder can be copied into `mods/`.

## Latest

- [v0.6.8](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.8): fixes disabled-card progress bar visibility. The HUD now uses a higher canvas layer with a valid z-index, is positioned lower to avoid the title bar, scans visible CardLibrary/CardGrid nodes more deeply, and no longer hides just because inactive Shop/Reward scenes exist in the tree.

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

## v0.6.8 feature summary

- Restores 26 old Defect cards to the Defect card pool and card library.
- Cross-run dynamic odds for Defect cards only: selected cards gain weight, exactly three-card Defect skip/reroll rewards subtract a total group weight, and each rarity is handled independently.
- Per-card disable state: disabled cards have reward weight 0 and persist in user-data storage.
- Large card/library UI shows dynamic odds text, disable/enable button, and greyed disabled cards.
- Hand-painted top disabled-card counter: 0-25 disabled cards are blue, 26-35 are red.
- `Data/Portraits/*.png` is included for CardBeautify's restored old Defect art.
- No BaseLib dependency.

## Install

Download `BetterDefect-v*.zip` from Releases, unzip it, and copy the `BetterDefect` folder into the game's `mods` folder.
