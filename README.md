# BetterDefect

Slay the Spire 2 BetterDefect mod. It restores 26 old Defect cards and adds cross-run dynamic reward odds, restored old Defect portraits for CardBeautify, card-library disable controls, a 35-point historical card-version upgrade system, and the Fission orb visual fix.

Compatibility: Android v103 and current PC builds. Download from GitHub Releases; each release asset is an install-ready zip whose `BetterDefect` folder can be copied into `mods/`.

## Latest

- [v0.10.5](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.10.5): fixes encyclopedia controls remaining over the card-detail overlay. Upgrade and disable controls are removed while details are open and restored on return; Android uses the lightweight library watcher instead of fragile redundant native UI detours. Verified live on PC v0.107.1 and REDMI K80 Pro / Android v0.103.2.

- [v0.10.0](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.10.0): adds 16 user-approved optional uncommon-card transformations for Chaos, Double Energy, Fight Through, Skim, Tempest, White Noise, FTL, Null, Refract, Feral, Hailstorm, Iteration, Loop, Smokestack, Storm and Subroutine. Base/upgraded values, combat behavior and Chinese descriptions are synchronized. PC v0.107.1 and Android v0.103.2 compile successfully; offline regression audit 135/135.

- [v0.9.4](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.9.4): fixes transformed Tesla Coil text. Normal: 3 damage and trigger every Lightning passive once. Upgraded: 4 damage and trigger every Lightning passive twice. Behavior is unchanged and matches the corrected text.

- [v0.9.3](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.9.3): fixes encyclopedia upgrade/odds controls leaking into combat, shop, deck and pile screens after visiting the encyclopedia. It requires current-scene ownership and synchronously strips controls from pooled card nodes on exit. Card art remains active. Offline audit 117/117; Android v0.103.2 startup and encyclopedia transition verified.

- [v0.9.2](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.9.2): fixes restored StS1 power status icons showing the red `NOPE` placeholder, adds smart descriptions for all six custom powers, and uses an Android-specific final-texture hook to remain stable on v103 ARM64. Verified 115/115 offline, PC v0.107.1, and REDMI K80 Pro / Android v0.103.2.

- [v0.9.1](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.9.1): fixes Tesla Coil and Shatter descriptions to match their actual orb behavior, and fixes Reprogram+ so Focus loss remains 1. Dual-target audit 96/96; PC v0.107.1 encyclopedia validation passed.

- [v0.9.0](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.9.0): adds 12 optional `??????` transformations for Barrage, Beam Cell, Charge Battery, Cold Snap, Go for the Eyes, Gunk Up, Leap, Lightning Rod, Sweeping Beam, Uproar, Recursion and Streamline. They use the existing persistent 35-point encyclopedia budget. PC v0.107.1 live startup/UI validation passed; dual-target source/binary audit 96/96.

- [v0.8.8](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.8): fixes encyclopedia-only disable/upgrade controls leaking into card details and later non-encyclopedia screens. Controls now require the exact active `NCardLibraryGrid`, hide synchronously before pooled-card reuse, and restore when returning to the encyclopedia list. Verified on PC v0.107.1 and REDMI K80 Pro / Android v0.103.2; offline audit 81/81.

- [v0.8.7](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.7): fixes `Shatter / 打碎` on Android v103 by removing the PC-only `ICombatState` metadata dependency from all-opponent targeting. The mobile build passed 76/76 offline checks, contains zero `ICombatState` type references, and was live-tested on v0.103.2: Shatter dealt its 11 damage, evoked Lightning twice for 8+8, completed normally, and produced no crash.

## History

- [v0.8.6](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.6): merged duplicate `ModelDb.Init` detours to reduce intermittent Android ARM64 startup aborts.

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
- [v0.6.22](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.6.22): caches restored-card portrait checks, card-type/key lookups, gameplay reflection and encyclopedia UI work to reduce Android frame spikes.
- [v0.7.0](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.7.0): adds the 35-point historical card-version upgrade system; its first archive used a Windows backslash directory record and is superseded by v0.7.1 for Android settings-page importing.
- [v0.7.1](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.7.1): rebuilds the archives with standard `/` ZIP paths and no ambiguous backslash directory entries.
- [v0.8.0](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.0): audits and fixes restored-card behavior routes, including Electrodynamics, Recycle, Lock-On and Static Discharge.
- [v0.8.1](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.1): fixes description/effect consistency for Rocket Punch, Tesla Coil, Fuel, Scrape, Fission, Core Surge and Amplify.
- [v0.8.2](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.2): adds the Android v103 startup guard while retaining all v0.8.1 fixes.
- [v0.8.3](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.3): fixes BetterDefect controls leaking into the in-run master-deck screen.
- [v0.8.4](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.4): fixes the encyclopedia HUD lifecycle.
- [v0.8.5](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.8.5): consolidates Android Pool/Rarity and card-version detours and removes unsafe redundant hooks.

## v0.8.7 Android Shatter fix

- Replaces Shatter's direct PC `AttackCommand.TargetingAllOpponents(ICombatState)` call with a runtime adapter that accepts Android v103's concrete `CombatState` signature and current PC builds' interface signature.
- Uses the same cross-version opponent lookup for Electrodynamics' all-target Lightning behavior.
- Keeps the v0.105 historical Shatter effect: 11 damage to all enemies, then evokes every orb twice.
- REDMI K80 Pro / Android v0.103.2 live test: `SHATTER` played successfully, multi-target attack completed, one Lightning orb evoked twice for 8+8, process remained alive, and the crash buffer stayed empty.
- Offline audit: 76/76. Compiled DLL audit: zero `MegaCrit.Sts2.Core.Combat.ICombatState` type references.

## v0.8.6 Android startup stability

- Merges the two `ModelDb.Init` postfixes into one native detour.
- Keeps the required localization initialization hook, avoiding missing `BD_*` card text on v103.
- Works with CardBeautify v0.4.9 and 败者食尘 v0.2.4, which also reduce Android startup detours.
- Final REDMI K80 Pro / v0.103.2 verification: 5/5 cold starts, 0 SIGABRT/SIGSEGV, 26/26 restored models, 114 Defect cards, no `LocException`.
- Offline regression audit: 71/71.

## v0.8.4 card-library HUD lifecycle fix

- Binds the segmented 35-point HUD to the exact `NCardLibrary` instance that requested it.
- Follows `NSubmenu.OnScreenVisibilityChange`, so temporarily hidden libraries cannot leave the HUD above another screen and re-shown libraries restore it immediately.
- Removes global scene-tree name/visibility scans and validates only the bound library, reducing extra Android traversal work.
- Refreshes the title when disabled/upgraded counts change even if their combined point total stays the same.
- Android v0.103.2 live test: main menu hidden, compendium landing page hidden, card library visible, leaving hidden, re-entry visible again.
- Offline audit: 72/72. PC v107.1 and mobile v103 compile with 0 errors.

## v0.8.3 in-run deck UI scope fix

- Fixes the disable/enable button, historical-version button, dynamic-odds text and disabled grey mask appearing in the in-run master-deck view.
- The in-run `NDeckViewScreen` and the encyclopedia share `NCardLibraryGrid`; the row-assignment hook now validates the owning screen instead of trusting the grid type alone.
- The generic `NGridCardHolder.SetIsPreviewingUpgrade` refresh no longer re-injects library controls after a non-library cleanup.
- Android v0.103.2 live test opened the Defect encyclopedia first, confirmed all library controls, then continued the current run and confirmed the master deck contained no BetterDefect controls.
- Offline audit: 69/69. PC v107.1 and mobile v103 compile with 0 errors.

## v0.8.2 Android startup guard

- Android v103 compiles out the Harmony detour for `NCard.Model`'s setter; the encyclopedia continues to use concrete `NCardLibraryGrid` refresh hooks.
- PC v107.1 retains the setter hook.
- All v0.8.1 description/effect consistency fixes are retained.
- Offline audit: 67/67. Both targets compile with 0 errors and were decompiled to confirm the platform-specific `Prepare()` result.
- Android v0.103.2 live verification reached startup Stage 14 and showed 114 Defect cards, dynamic odds, disable controls/grey mask, art controls, historical-version controls and the segmented 35-point HUD.

## v0.8.1 description/effect consistency audit

- Rocket Punch text distinguishes the base “until played or this turn ends” behavior from the v0.100 “until played” behavior.
- Tesla Coil only previews two Lightning triggers when the v0.105 historical switch and normal card upgrade are both active.
- Fuel hides its draw line under Compact's v0.108 behavior and restores the 1/2-card line under the v0.99 switch.
- Scrape explains whether it checks the card-local energy cost or the current final energy cost.
- Fission shows only the current remove/evoke behavior; Fission and Core Surge no longer duplicate the engine-generated Exhaust line.
- Amplify is removed at player-turn end and consumes one stack via `AfterModifyingCardPlayCount`, matching its “this turn” description.
- The offline regression suite now contains 66 checks. Both target DLLs were separately compiled and decompiled; see `docs/reports/`.

## v0.8.0 restored-card audit

- Electrodynamics patches Lightning's shared `ApplyLightningDamage` method, so end-turn passive triggers and evokes both use all hittable opponents.
- Recycle uses the native hand-selection flow and refunds an X-cost card using the player's current remaining energy.
- Lock-On returns `1.5m` as a multiplier and ticks down once per enemy turn rather than once per orb hit.
- Static Discharge filters out poison, orb/relic damage and other HP-loss effects.
- The offline regression suite checks all 26 restored registrations, the defining values/effect routes of all 22 recreated cards, and all 14 historical-version mappings.
- Current PC and v103 DLLs were separately compiled and decompiled. See `docs/reports/` for the 59/59 source audit and compiled-IL evidence.

## v0.7.1 Android importer fix

- The Android v103 settings importer normalizes `\\` to `/`, but checks `ZipEntry.isDirectory()` on the original entry name.
- The v0.7.0 PowerShell archive contained `BetterDefect\\Data\\` as a directory entry. Android did not recognize that original backslash entry as a directory and attempted to open `mods/BetterDefect/Data/` as a file, producing the import failure.
- v0.7.1 contains only file entries, all with standard forward-slash paths. An importer-equivalent extraction simulation now succeeds.
- If v0.7.0 already failed once, remove the partial BetterDefect entry/folder in the settings page before importing v0.7.1.

## v0.7.0 historical card-version system

- The feature is available only in the encyclopedia card library and only for the 14 listed Defect cards.
- Card disabling and historical-version switches share a persistent 35-point budget. One disabled card or one enabled historical switch costs one point.
- The top hand-painted segmented bar shows `disabled X · upgraded Y`; 0-25 points are blue and 26-35 points are red.
- Version switches, disabled states, card-art choices and dynamic odds survive restarts and DLL replacement.
- These are global historical balance-version switches. Normal per-copy smithing upgrades still work independently.
- Version targets: Hotfix + -> v0.99; Rocket Punch base v0.99 / switch v0.100; Voltaic -> v0.99; Hyperbeam -> v0.109; Shatter base v0.108 / switch v0.105; Tesla Coil -> v0.105; Uproar -> v0.105; Fusion -> v0.106; Synthesis -> v0.106; Compact base v0.108 / switch v0.99; Momentum Strike -> v0.108; Scrape -> v0.108; Sunder -> v0.109; Trash to Treasure base v0.109 / switch v0.99.
- The v0.7.0 card-library refresh rebuilds the base game's cached upgraded preview, so switching `View Upgrades` immediately reflects the selected historical version instead of showing a stale card clone.

## Core feature summary

- Restores 26 old Defect cards to the Defect card pool and card library.
- Defect starter deck replacement is back: one starting `StrikeDefect` is replaced by `BallLightning`, with a duplicate guard so the patch will not replace multiple Strikes if it runs again.
- Mobile draw/play hot path optimized: outside the encyclopedia/card-library screen, `NCard.UpdateVisuals` no longer runs Defect-card pool checks, reflection-based description cleanup, dynamic-odds text injection, disable button creation, or grey-mask work.
- The segmented point HUD is bound to the exact visible `NCardLibrary`; it follows submenu visibility events and never performs global scene-tree visibility scans.
- Cross-run dynamic odds for Defect cards only: selected cards gain weight, exactly three-card Defect skip/reroll rewards subtract a total group weight, and each rarity is handled independently.
- Encyclopedia/card-library UI shows `动态出率：x.xx` in Chinese without mojibake.
- In the encyclopedia/card-library only, Defect cards have a mobile-sized `禁用出率` / `启用出率` button; disabled cards are excluded from reward replacement, show `0.00x（已禁用）`, and get a grey aligned mask.
- Non-encyclopedia card views remove BetterDefect's disable button, grey mask, and dynamic-odds text. v0.6.18 also removes the old broad `CardLibrary` namespace/name fallback and explicitly excludes `NCardPileScreen`, deck, draw pile, discard pile, exhaust pile and shop deck views.
- v0.6.22 keeps the v0.6.19 no-global `NCard.UpdateVisuals` design and the v0.6.21 strict `NCardLibrary` verification, while adding extra mobile caches for card art path checks, restored-card type checks, dynamic-odds card keys, PowerCmd/orb reflection and encyclopedia label/style work.
- The HUD performs a low-frequency direct validity check only while visible, against its already-bound `NCardLibrary` instance.
- The top segmented disabled-card counter shows 0-25 in blue and 26-35 in red, and remains visible only inside the encyclopedia/card-library screen.
- `Data/Portraits/*.png` is included for CardBeautify's restored old Defect art.
- Defect-card type checks and restored old-card list generation are cached to reduce repeated reflection/model lookups on Android.
- Restored StS1 powers use valid built-in status textures instead of the red `NOPE` placeholder, with complete smart descriptions.
- No BaseLib dependency.

## Install

Download `BetterDefect-v0.10.0-Mobile-v103.zip` for mobile v103 or `BetterDefect-v0.10.0-PC-v107.1.zip` for PC v107.1, unzip/import it, and copy the included `BetterDefect` folder into the game's `mods` folder.

The repository now includes the C# source in `src/`, the offline regression checker in `tests/`, and the v103 compatibility source-preparation helper in `tools/`. Card portrait assets remain in the release archives.


## v0.10.3

- Fix Android v103 startup after the encyclopedia-control repair by skipping the ARM64-unsafe LightningOrb Electrodynamics detour on mobile.
- Keeps the v103 Hailstorm turn-end hook, resilient per-hook initialization, and active encyclopedia grid hooks.
- Verified on a connected Android v103 device: BetterDefect initializes, 26 old Defect cards inject, the 114-card Defect pool is restored, and the encyclopedia card-point HUD plus in-card controls are visible.

## v0.10.1

- Fix encyclopedia card-point HUD and enable/disable odds plus upgrade buttons disappearing.
- Cause: v0.10.0 treated any visible card node outside the encyclopedia grid as a non-library context, so the real compendium could be rejected. The guard now validates only the exact NCardLibraryGrid owned by the active card library.
- Keeps the v0.10.0 uncommon-card transformation set and the shared 35-point budget.
