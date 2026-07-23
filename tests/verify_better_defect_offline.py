#!/usr/bin/env python3
"""Offline structural regression checks for BetterDefect.

These checks deliberately do not start Slay the Spire 2.  They verify the
restored-card registry, every recreated StS1 card's defining values/behavior,
the four audited power fixes, 14 historical-version routes, 11 additional
custom common-card transformations, and 16 custom uncommon-card transformations.
"""

from __future__ import annotations

import argparse
import datetime as dt
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT if (ROOT / "BetterDefectCode").is_dir() else ROOT / "src"


def class_body(source: str, class_name: str) -> str:
    match = re.search(rf"\bclass\s+{re.escape(class_name)}\b", source)
    if not match:
        raise AssertionError(f"class not found: {class_name}")
    start = source.find("{", match.end())
    if start < 0:
        raise AssertionError(f"class body not found: {class_name}")
    depth = 0
    for index in range(start, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[start + 1 : index]
    raise AssertionError(f"unclosed class: {class_name}")


def read(relative_path: str) -> str:
    return (PROJECT / relative_path).read_text(encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", type=Path)
    parser.add_argument("--binary", type=Path, action="append", default=[])
    args = parser.parse_args()

    cards = (PROJECT / "BetterDefectCode" / "CardsAndPowers.cs").read_text(encoding="utf-8")
    old_cards = (PROJECT / "BetterDefectCode" / "OldDefectCards.cs").read_text(encoding="utf-8")
    versions = (PROJECT / "BetterDefectCode" / "CardVersionUpgrades.cs").read_text(encoding="utf-8")
    localization = (PROJECT / "BetterDefectCode" / "Localization.cs").read_text(encoding="utf-8")
    power_icons = (PROJECT / "BetterDefectCode" / "PowerIcons.cs").read_text(encoding="utf-8")
    manifest = (PROJECT / "BetterDefect.json").read_text(encoding="utf-8")

    passed: list[str] = []
    failed: list[str] = []

    def check(name: str, condition: bool, detail: str = "") -> None:
        if condition:
            passed.append(name)
        else:
            failed.append(name + (f": {detail}" if detail else ""))

    registry_match = re.search(
        r"private static readonly Type\[\] CardTypes\s*=\s*\{(?P<body>.*?)\};",
        old_cards,
        re.S,
    )
    registered = re.findall(r"typeof\((\w+)\)", registry_match.group("body") if registry_match else "")
    hidden = ["HelloWorld", "Rebound", "RipAndTear", "Stack"]
    recreated = [
        "BdRecursion", "BdSteamBarrier", "BdStreamline", "BdAggregate",
        "BdAutoShields", "BdBlizzard", "BdBullseye", "BdConsume",
        "BdDoomAndGloom", "BdForceField", "BdHeatsinks", "BdMelter",
        "BdRecycle", "BdReinforcedBody", "BdReprogram", "BdSelfRepair",
        "BdStaticDischarge", "BdAmplify", "BdCoreSurge", "BdElectrodynamics",
        "BdFission", "BdThunderStrike",
    ]
    check("restored registry contains exactly 26 cards", registered == hidden + recreated, repr(registered))

    # Tokens describe the defining StS1 values and effect route for every
    # recreated card.  This is intentionally stricter than a class-count test.
    card_specs: dict[str, tuple[str, ...]] = {
        "BdRecursion": ("base(1, CardType.Skill, CardRarity.Common", "OrbCmd.EvokeNext", "OrbCmd.Channel<LightningOrb>", "EnergyCost.UpgradeBy(-1)"),
        "BdSteamBarrier": ("new BlockVar(6", "base(0, CardType.Skill, CardRarity.Common", "BaseValue - 1", "Block.UpgradeValueBy(2)"),
        "BdStreamline": ("new DamageVar(15", "base(2, CardType.Attack, CardRarity.Common", "AddThisCombat(-1", "Damage.UpgradeValueBy(5)"),
        "BdAggregate": ('new DynamicVar("Divisor", 4)', "DrawPile.Cards.Count / divisor", 'DynamicVars["Divisor"].UpgradeValueBy(-1)'),
        "BdAutoShields": ("new BlockVar(11", "Owner.Creature.Block <= 0", "Block.UpgradeValueBy(4)"),
        "BdBlizzard": ("new DamageVar(2", "FrostChanneled", "Bd.Enemies(this)", "Damage.UpgradeValueBy(1)"),
        "BdBullseye": ("new DamageVar(8", 'new DynamicVar("LockOn", 2)', "ApplyPower<BdLockOnPower>", "Damage.UpgradeValueBy(3)", 'DynamicVars["LockOn"].UpgradeValueBy(1)'),
        "BdConsume": ('new DynamicVar("Focus", 2)', "ApplyPower<FocusPower>", "OrbCmd.RemoveSlots(Owner, 1)", 'DynamicVars["Focus"].UpgradeValueBy(1)'),
        "BdDoomAndGloom": ("new DamageVar(10", "Bd.DamageAll", "OrbCmd.Channel<DarkOrb>", "Damage.UpgradeValueBy(4)"),
        "BdForceField": ("new BlockVar(12", "base(4, CardType.Skill", "PowerCardsPlayed", "Block.UpgradeValueBy(4)"),
        "BdHeatsinks": ('new DynamicVar("Draw", 1)', "ApplyPower<BdHeatsinksPower>", 'DynamicVars["Draw"].UpgradeValueBy(1)'),
        "BdMelter": ("new DamageVar(10", "CreatureCmd.LoseBlock", "Bd.Damage", "Damage.UpgradeValueBy(4)"),
        "BdRecycle": ("CardSelectCmd.FromHand", "ExhaustSelectionPrompt", "Bd.CostForEnergy(victim)", "CardCmd.Exhaust", "GainEnergy(energy)", "EnergyCost.UpgradeBy(-1)"),
        "BdReinforcedBody": ("HasEnergyCostX", "new BlockVar(7", "ResolveEnergyXValue", "Block.UpgradeValueBy(2)"),
        "BdReprogram": ('new DynamicVar("Focus", 1)', "new PowerVar<StrengthPower>(1)", "new PowerVar<DexterityPower>(1)", "-DynamicVars", "ApplyPower<StrengthPower>", "ApplyPower<DexterityPower>"),
        "BdSelfRepair": ("new HealVar(7)", "ApplyPower<BdSelfRepairPower>", "Heal.UpgradeValueBy(3)"),
        "BdStaticDischarge": ('new DynamicVar("Amount", 1)', "ApplyPower<BdStaticDischargePower>", 'DynamicVars["Amount"].UpgradeValueBy(1)'),
        "BdAmplify": ('new DynamicVar("Amount", 1)', "CardKeyword.Exhaust", "ApplyPower<BdAmplifyPower>", 'DynamicVars["Amount"].UpgradeValueBy(1)'),
        "BdCoreSurge": ("new DamageVar(11", "CardKeyword.Exhaust", "ApplyPower<ArtifactPower>", "Damage.UpgradeValueBy(4)"),
        "BdElectrodynamics": ('new DynamicVar("Amount", 2)', "ApplyPower<BdElectrodynamicsPower>", "OrbCmd.Channel<LightningOrb>", 'DynamicVars["Amount"].UpgradeValueBy(1)'),
        "BdFission": ("base(0, CardType.Skill, CardRarity.Rare", "CardKeyword.Exhaust", "OrbCmd.EvokeNext", "RemoveOrbWithoutEvoke", "GainEnergy(1)", "CardPileCmd.Draw"),
        "BdThunderStrike": ("new DamageVar(7", "LightningChanneled", "Bd.RandomEnemy", "Damage.UpgradeValueBy(2)"),
    }
    for class_name, tokens in card_specs.items():
        try:
            body = class_body(cards, class_name)
            missing = [token for token in tokens if token not in body]
            check(f"{class_name} defining effect/value audit", not missing, f"missing {missing}")
        except AssertionError as exc:
            check(f"{class_name} defining effect/value audit", False, str(exc))

    recycle_helper = class_body(cards, "Bd")
    check("Recycle X-cost uses current remaining energy", "EnergyCost.CostsX" in recycle_helper and "PlayerCombatState.Energy" in recycle_helper)

    static_power = class_body(cards, "BdStaticDischargePower")
    check("Static Discharge only accepts powered Move damage", "ValueProp.Move" in static_power and "ValueProp.Unpowered | ValueProp.Unblockable" in static_power)

    electro_power = class_body(cards, "BdElectrodynamicsPower")
    electro_patch = class_body(cards, "BdElectrodynamicsLightningTargetPatch")
    check("Electrodynamics no longer adds damage only after evoke", "AfterOrbEvoked" not in electro_power)
    check("Electrodynamics patches Lightning passive and evoke common path", "ApplyLightningDamage" in electro_patch and "Bd.Opponents" in electro_patch and "CreatureCmd.Damage" in electro_patch)

    lock_on = class_body(cards, "BdLockOnPower")
    check("Lock-On returns multiplier 1.5 instead of multiplied damage", "return 1.5m;" in lock_on and "amount * 1.5m" not in lock_on)
    check("Lock-On expires by enemy turn instead of per hit", "AfterSideTurnEnd" in lock_on and "PowerCmd.TickDownDuration" in lock_on and "AfterDamageReceived" not in lock_on)

    power_specs = {
        "BdHeatsinksPower": ("AfterCardPlayed", "CardType.Power", "CardPileCmd.Draw"),
        "BdSelfRepairPower": ("AfterCombatVictory", "CreatureCmd.Heal"),
        "BdAmplifyPower": ("ModifyCardPlayCount", "playCount + 1", "AfterModifyingCardPlayCount", "PowerCmd.Decrement", "AfterSideTurnEnd", "PowerCmd.Remove"),
    }
    for class_name, tokens in power_specs.items():
        body = class_body(cards, class_name)
        missing = [token for token in tokens if token not in body]
        check(f"{class_name} effect route audit", not missing, f"missing {missing}")

    version_list_match = re.search(r"VersionedCardTypes\s*=\s*\[(?P<body>.*?)\];", versions, re.S)
    version_types = re.findall(r"typeof\((\w+)\)", version_list_match.group("body") if version_list_match else "")
    expected_version_types = [
        "Hotfix", "RocketPunch", "Voltaic", "Hyperbeam", "Shatter", "TeslaCoil", "Uproar",
        "Fusion", "Synthesis", "Compact", "MomentumStrike", "Scrape", "Sunder", "TrashToTreasure",
        "Barrage", "BeamCell", "ChargeBattery", "ColdSnap", "GoForTheEyes", "GunkUp", "Leap",
        "LightningRod", "SweepingBeam", "BdRecursion", "BdStreamline",
        "Chaos", "DoubleEnergy", "FightThrough", "Skim", "Tempest", "WhiteNoise",
        "Ftl", "Null", "Refract", "Feral", "Hailstorm", "Iteration", "Loop",
        "Smokestack", "Storm", "Subroutine",
    ]
    check("card-transformation registry contains exactly 41 cards", version_types == expected_version_types, repr(version_types))
    for card_id in (
        "HOTFIX", "ROCKET_PUNCH", "VOLTAIC", "HYPERBEAM", "SHATTER", "TESLA_COIL", "UPROAR",
        "FUSION", "SYNTHESIS", "COMPACT", "MOMENTUM_STRIKE", "SCRAPE", "SUNDER", "TRASH_TO_TREASURE",
    ):
        check(f"historical mapping exists for {card_id}", f'CARD.{card_id}' in versions)

    behavior_checks = {
        "Hotfix v0.99 removes Exhaust": "SetKeyword(card, CardKeyword.Exhaust, !upgradedVersion && !plus)",
        "Rocket Punch v0.100 persists zero cost until played": "SetUntilPlayed(0)",
        "Shatter double-evokes every orb": "OrbCmd.EvokeNext(choiceContext, card.Owner, dequeue: false)",
        "Tesla Coil v0.105 upgraded card triggers Lightning twice": "card.IsUpgraded && BdCardVersionUpgrades.IsVersionEnabled(card)",
        "Compact v0.99 Fuel draws cards": 'SetDynamic(card, "Cards", plus ? 2m : 1m)',
        "Scrape v0.108 uses all cost modifiers": "useV108 ? CostModifiers.All : CostModifiers.Local",
        "Trash to Treasure v0.99 upgrade is Innate": "SetKeyword(card, CardKeyword.Innate, plus && upgradedVersion)",
        "Barrage custom route applies temporary Focus": "var temporaryFocus = card.DynamicVars.Damage.BaseValue",
        "Beam Cell custom route applies BetterDefect Lock-On": "Bd.ApplyPower<BdLockOnPower>",
        "Charge Battery custom route draws next turn": "Bd.ApplyPower<DrawCardsNextTurnPower>",
        "Cold Snap custom route channels two Frost": "await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);\n        await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);",
        "Go for the Eyes custom route always applies Weak": "PlayGoForTheEyes",
        "Gunk Up custom route generates Slimed into hand": "AddGeneratedCardToCombat(slimed, PileType.Hand",
        "Leap custom route becomes zero cost for combat": "card.EnergyCost.SetThisCombat(0)",
        "Lightning Rod custom route channels now and once next turn": "Bd.ApplyPower<LightningRodPower>",
        "Sweeping Beam custom normal upgrade draws two": 'SetDynamic(card, "Cards", upgradedVersion && plus ? 2m : 1m)',
        "Uproar custom route prioritizes current two-cost attacks": "playableAttacks.Where(IsCurrentTwoCost)",
    }
    for name, token in behavior_checks.items():
        check(name, token in versions)
    check(
        "Barrage transformed temporary Focus is two and upgrades to three",
        'upgradedVersion ? plus ? 3m : 2m : plus ? 7m : 5m' in versions
        and 'UpgradeDynamicTo(card, "Damage", upgradedVersion ? 3m : 7m)' in versions,
    )
    barrage_play = class_body(versions, "BdCustomCommonCardPlayPatch")
    barrage_start = barrage_play.find("private static async Task PlayBarrage")
    barrage_end = barrage_play.find("private static async Task PlayBeamCell", barrage_start)
    barrage_route = barrage_play[barrage_start:barrage_end]
    gain_at = barrage_route.find("temporaryFocus,")
    passive_at = barrage_route.find("OrbCmd.Passive")
    remove_at = barrage_route.find("Bd.ModifyPowerAmount")
    check(
        "Barrage gains Focus, triggers each orb once, then removes Focus",
        gain_at >= 0 and passive_at > gain_at and remove_at > passive_at
        and "foreach (var orb in orbs)" in barrage_route
        and "GetPower<FocusPower>()" in barrage_route
        and "-temporaryFocus" in barrage_route
        and "for (var repeat" not in barrage_route,
    )
    check(
        "Lightning Rod transformed Block is five and upgrades to six",
        'upgradedVersion\n                    ? plus ? 6m : 5m' in versions
        and 'upgradedVersion ? plus ? 6m : 5m : plus ? 7m : 4m' in versions,
    )
    uncommon_behavior_checks = {
        "Chaos prioritizes missing orb types": "missing.Count > 0 ? missing : canonical",
        "Double Energy draws one card": "PlayDoubleEnergy",
        "Fight Through generates Dazed": "Bd.CreateCard<Dazed>",
        "Skim discards before drawing": "PlaySkim",
        "Tempest draws when overflow evokes Lightning": "if (evokedLightning)",
        "White Noise offers three powers": "PlayWhiteNoise",
        "FTL fallback applies Lock-On": "PlayFtl",
        "Null checks pre-existing Weak": "var alreadyWeak = cardPlay.Target.HasPower<WeakPower>()",
        "Refract costs two with Glass": "Math.Min(originalCost, 2m)",
        "Feral custom card costs two and upgrades to one": 'SetEnergy(card, plus ? 1 : 2)',
        "Feral custom power returns any zero-energy card": "BdCustomFeralPowerResultPatch",
        "Hailstorm scales with Frost count": "power.Amount * frostCount",
        "Iteration exhausts the first drawn status": "FinishAndExhaust",
        "Loop triggers both edge orbs": "BdCustomLoopPowerPatch",
        "Smokestack draws on its first trigger": "BdCustomSmokestackPowerPatch",
        "Storm gains Innate when transformed": "SetKeyword(card, CardKeyword.Innate, upgradedVersion)",
        "Subroutine draws on its first trigger": "BdCustomSubroutinePowerPatch",
    }
    for name, token in uncommon_behavior_checks.items():
        check(name, token in versions)
    smokestack_patch = class_body(versions, "BdCustomSmokestackPowerPatch")
    subroutine_patch = class_body(versions, "BdCustomSubroutinePowerPatch")
    check(
        "Smokestack patch accepts both Android v103 and PC callback arguments",
        "object[] __args" in smokestack_patch
        and "bool addedByPlayer" in smokestack_patch
        and "Player creator" in smokestack_patch
        and "generatedByOwner" in smokestack_patch,
    )
    check(
        "Subroutine patch avoids callback parameter-name drift",
        subroutine_patch.count("object[] __args") >= 2
        and "__args[1] as CardPlay" in subroutine_patch
        and "__args[0] is not PlayerChoiceContext choiceContext" in subroutine_patch,
    )
    check("Recursion custom route double-evokes the leftmost orb", "OrbCmd.EvokeNext(choiceContext, Owner, dequeue: false)" in cards)
    check("Streamline custom route discounts every copy", "AllCards.OfType<BdStreamline>()" in cards)

    check("Recycle localization describes selection", "选择并[gold]消耗[/gold] 1 张手牌" in localization)
    check("Electrodynamics localization covers passive and evoke", "被动与激发伤害会命中所有敌人" in localization)
    check("Fission description switches remove/evoke with normal upgrade", "{IfUpgraded:show:[gold]激发[/gold]所有充能球。|移除所有充能球。}" in localization)
    check("Core Surge and Fission rely on the real Exhaust keyword text", '["cards/BD_CORE_SURGE.description"]' in localization and '["cards/BD_FISSION.description"]' in localization and "\\n[gold]消耗[/gold]。" not in localization)
    check("Rocket Punch description follows its historical behavior switch", 'rocketV100' in localization and "直到打出或当前回合结束" in localization)
    check("Tesla Coil description switches transformed passive count and keeps dynamic damage", 'teslaV105' in localization and "造成{Damage:diff()}点伤害" in localization and "充能球被动{IfUpgraded:show:两次|一次}" in localization)
    check("Shatter description explicitly says every orb is evoked twice", '["SHATTER.description"]' in localization and "[gold]激发[/gold]所有充能球两次" in localization)
    check("Fuel description hides drawing when Compact uses v0.108 behavior", 'compactV099' in localization and '["FUEL.description"]' in localization)
    check("Scrape description distinguishes local and final energy cost", 'scrapeV108' in localization and "按当前最终耗能计算" in localization and "按卡牌自身耗能计算" in localization)
    check("custom transformations are labelled exactly", '"改造：自定义"' in versions and 'targetLabel.StartsWith("改造："' in read("BetterDefectCode/DynamicOddsUi.cs"))
    check("custom common-card descriptions follow their switches", all(token in localization for token in (
        "barrageCustom", "beamCellCustom", "chargeBatteryCustom", "coldSnapCustom", "goForTheEyesCustom",
        "gunkUpCustom", "leapCustom", "lightningRodCustom", "sweepingBeamCustom", "uproarCustom",
        "recursionCustom", "streamlineCustom",
    )))
    check(
        "Barrage transformed description says temporary Focus and one passive trigger",
        "获得{Damage:diff()}点[gold]临时集中[/gold]，然后触发你的所有充能球的被动一次。" in localization,
    )
    check("custom uncommon-card descriptions follow their switches", all(token in localization for token in (
        "chaosCustom", "doubleEnergyCustom", "fightThroughCustom", "skimCustom", "tempestCustom",
        "whiteNoiseCustom", "ftlCustom", "nullCustom", "refractCustom", "feralCustom",
        "hailstormCustom", "iterationCustom", "loopCustom", "smokestackCustom", "stormCustom",
        "subroutineCustom",
    )))
    check("Feral custom text includes every zero-energy card type", '? "你每回合第一次打出的耗能为0' in localization and '的牌，会放回你的[gold]手牌[/gold]' in localization)
    check("Amplify text and power both expire this turn", "本回合下 {Amount" in localization and "AfterSideTurnEnd" in class_body(cards, "BdAmplifyPower"))
    check("Reprogram+ keeps Focus loss at one", 'DynamicVars["Focus"].UpgradeValueBy' not in class_body(cards, "BdReprogram") and 'DynamicVars.Strength.UpgradeValueBy(1)' in class_body(cards, "BdReprogram") and 'DynamicVars.Dexterity.UpgradeValueBy(1)' in class_body(cards, "BdReprogram"))
    ui = read("BetterDefectCode/DynamicOddsUi.cs")
    helper = (ROOT / "tools" / "prepare_v103_source.py").read_text(encoding="utf-8")
    check("Android skips unsafe NCard.Model setter detour", "DisableUnsafeAndroidSetterDetour = false" in ui and "DisableUnsafeAndroidSetterDetour = true" in helper)
    assign_patch = class_body(ui, "BdDynamicOddsCardLibraryGridAssignPatch")
    preview_refresh = re.search(
        r"internal static void ReapplyAfterUpgradePreviewRefresh\(NGridCardHolder holder\)(?P<body>.*?)\n    }",
        ui,
        re.S,
    )
    preview_body = preview_refresh.group("body") if preview_refresh else ""
    check(
        "library-grid row assignment validates the owning screen",
        "ApplyLibraryRowForGrid(__instance, assignedHolders)" in assign_patch
        and "ApplyLibraryRow(assignedHolders, assumeVerifiedLibrary: true)" not in assign_patch
        and "if (!IsCardLibraryContext(grid))" in ui,
    )
    check(
        "generic upgrade-preview refresh cannot re-inject run-deck controls",
        "ApplyLibraryCardUi(holder.CardNode);" in preview_body
        and "assumeLibrary: true" not in preview_body
        and '"DeckView"' in ui,
    )
    hud = read("BetterDefectCode/DynamicOddsStatsHud.cs")
    visibility_patch = class_body(ui, "BdDynamicOddsCardLibraryVisibilityPatch")
    check(
        "HUD follows NSubmenu visibility transitions",
        'HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NSubmenu), "OnScreenVisibilityChange")' in ui
        and "__instance is NCardLibrary library" in visibility_patch
        and "SyncLibraryVisibility(library)" in visibility_patch,
    )
    check(
        "HUD is bound to the exact visible card library",
        "private static NCardLibrary? _activeLibrary;" in hud
        and "var grid = BdDynamicOddsCardUi.GetLibraryGrid(library);" in hud
        and "BdDynamicOddsCardUi.IsCardLibraryContext(grid)" in hud
        and "ShowForLibrary(Node context)" in hud,
    )
    check(
        "HUD no longer uses global scene-tree visibility guesses",
        "ShouldShowFromTree" not in hud
        and "ShouldForceHideFromTree" not in hud
        and "HasRecentStatsContext" not in hud,
    )
    check(
        "encyclopedia controls require the exact owned live grid",
        "var ownedGrid = GetLibraryGrid(library);" in ui
        and "ReferenceEquals(ownedGrid, grid)" in ui
        and "IsVisibleInTreeStrict(library)" in ui
        and "IsVisibleInTreeStrict(grid)" in ui,
    )
    check(
        "card-detail popup invalidates encyclopedia control scope",
        "NGame.Instance?.InspectCardScreen" in ui
        and "inspectItem.IsVisibleInTree()" in ui,
    )
    check(
        "BetterDefect controls disappear synchronously before pooled-node release",
        "HideAndQueueFree(cardNode.GetNodeOrNull<Button>(ToggleButtonName))" in ui
        and "item.Visible = false" in ui
        and "control.MouseFilter = Control.MouseFilterEnum.Ignore" in ui,
    )
    check(
        "library watcher cleans controls on detail and exit transitions",
        "BdDynamicOddsCardUi.CleanupAllTouchedCards();" in hud
        and "if (_wasVisible || _library is not null)" in hud
        and "BdDynamicOddsCardUi.ApplyLibraryGrid(grid);" in hud,
    )
    for power_id in (
        "BD_HEATSINKS_POWER", "BD_SELF_REPAIR_POWER", "BD_STATIC_DISCHARGE_POWER",
        "BD_AMPLIFY_POWER", "BD_ELECTRODYNAMICS_POWER", "BD_LOCK_ON_POWER",
    ):
        check(f"{power_id} has a smart combat description", f'power/{power_id}.smartDescription' in localization or f'powers/{power_id}.smartDescription' in localization)
    for power_type in (
        "BdHeatsinksPower", "BdSelfRepairPower", "BdStaticDischargePower",
        "BdAmplifyPower", "BdElectrodynamicsPower", "BdLockOnPower",
    ):
        check(f"{power_type} redirects its missing power icon", f"typeof({power_type})" in power_icons)
    check("power icon redirect patches status and large icons", "PackedIconPath" in power_icons and "BigIconPath" in power_icons)
    check("power icon redirect validates bundled resources", "ResourceLoader.Exists(candidate)" in power_icons)
    check("Android patches final power texture getter", '[HarmonyPatch(typeof(PowerModel), "get_Icon")]' in power_icons)
    check("injected powers validate all six status textures", "ValidateInjectedStatusIcons" in power_icons and "BdPowerIconPathPatch.ValidateInjectedStatusIcons();" in read("BetterDefectCode/Patches.cs"))
    check("Android power-icon detour replaces beta portrait detour", "type == typeof(BdPowerIconPathPatch)" in read("BetterDefectCode/MainFile.cs") and "type == typeof(BetterDefectBetaPortraitPatch)" in read("BetterDefectCode/MainFile.cs"))
    hud = read("BetterDefectCode/DynamicOddsStatsHud.cs")
    check("manifest is v0.10.9", '"version":  "0.10.9"' in manifest)
    check("encyclopedia context is owned by the current scene", "IsUnderCurrentScene(library)" in ui)
    check("full pooled-card cleanup exists", "internal static void CleanupAllTouchedCards()" in ui)
    check("library watcher synchronously strips pooled controls", "CleanupAllTouchedCards();" in hud and "_library = null;" in hud)
    check("cross-version combat state uses reflection", 'AccessTools.Property(sourceType, "CombatState")' in cards)
    check("cross-version enemy targeting avoids direct CombatState typing", "TryTargetAllOpponents(object attackCommand, CardModel card)" in cards)
    check("Electrodynamics uses cross-version opponent lookup", "Bd.Opponents(orb.Owner.Creature)" in cards)
    check("source contains no direct model CombatState access", "card.CombatState" not in cards and "orb.CombatState" not in cards)
    check("Shatter uses cross-version all-opponent targeting", "Cards.Bd.TryTargetAllOpponents(attack, card)" in versions)
    check("Shatter no longer directly targets card.CombatState", ".TargetingAllOpponents(card.CombatState)" not in versions)

    for binary in args.binary:
        exists = binary.is_file() and binary.stat().st_size > 100_000
        check(f"compiled binary exists: {binary}", exists)

    lines = [
        "BetterDefect v0.10.9 offline audit",
        f"Timestamp: {dt.datetime.now().astimezone().isoformat(timespec='seconds')}",
        "Mode: source/registry/behavior-route/binary checks only; game was not launched",
        f"Passed: {len(passed)}",
        f"Failed: {len(failed)}",
        "",
        "PASS",
        *[f"  [OK] {name}" for name in passed],
    ]
    if failed:
        lines.extend(["", "FAIL", *[f"  [FAIL] {name}" for name in failed]])
    report = "\n".join(lines) + "\n"
    print(report, end="")
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(report, encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
