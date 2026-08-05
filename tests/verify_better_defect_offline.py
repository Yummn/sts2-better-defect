#!/usr/bin/env python3
"""Offline structural regression checks for BetterDefect.

These checks deliberately do not start Slay the Spire 2.  They verify the
restored-card registry, every recreated StS1 card's defining values/behavior,
the audited power fixes, 10 historical-version routes, 13 custom common-card
transformations, 29 custom uncommon-card transformations, and 17 custom
rare-card transformations.
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
    parser.add_argument(
        "--mobile-binary",
        type=Path,
        action="append",
        default=[],
        help="Android v103 binary; additionally reject PC-only ICombatState metadata",
    )
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
        "BdStaticDischarge", "BdSeek", "BdCoreSurge", "BdElectrodynamics",
        "BdFission", "BdThunderStrike",
    ]
    check("restored registry contains exactly 26 cards", registered == hidden + recreated, repr(registered))

    # Tokens describe the defining StS1 values and effect route for every
    # recreated card.  This is intentionally stricter than a class-count test.
    card_specs: dict[str, tuple[str, ...]] = {
        "BdRecursion": ("base(1, CardType.Skill, CardRarity.Common", "OrbCmd.EvokeNext", "OrbCmd.Channel<LightningOrb>", "EnergyCost.UpgradeBy(-1)"),
        "BdSteamBarrier": ("new BlockVar(8", "base(0, CardType.Skill, CardRarity.Common", "BaseValue - 1", "Block.UpgradeValueBy(4)"),
        "BdStreamline": ("new DamageVar(15", "base(2, CardType.Attack, CardRarity.Common", "AddThisCombat(-1", "Damage.UpgradeValueBy(5)"),
        "BdAggregate": ('new DynamicVar("Divisor", 4)', "DrawPile.Cards.Count / divisor", 'DynamicVars["Divisor"].UpgradeValueBy(-1)'),
        "BdAutoShields": ("new BlockVar(10", "OrbCmd.Channel<FrostOrb>", "Owner.Creature.Block <= 0", "Block.UpgradeValueBy(5)"),
        "BdBlizzard": ("new DamageVar(2", "FrostChanneled", "Bd.Enemies(this)", "Damage.UpgradeValueBy(1)"),
        "BdBullseye": ("new DamageVar(8", 'new DynamicVar("LockOn", 2)', "ApplyPower<BdLockOnPower>", "Damage.UpgradeValueBy(3)", 'DynamicVars["LockOn"].UpgradeValueBy(1)'),
        "BdConsume": ('new DynamicVar("Focus", 2)', "ApplyPower<FocusPower>", "OrbCmd.RemoveSlots(Owner, 1)", 'DynamicVars["Focus"].UpgradeValueBy(1)'),
        "BdDoomAndGloom": ("new DamageVar(10", "Bd.DamageAll", "OrbCmd.Channel<DarkOrb>", "Damage.UpgradeValueBy(4)"),
        "BdForceField": ("CardKeyword.Retain", "new BlockVar(12", "base(4, CardType.Skill", "PowerCardsPlayed", "Block.UpgradeValueBy(4)"),
        "BdHeatsinks": ('new DynamicVar("Draw", 1)', "ApplyPower<BdHeatsinksPower>", 'DynamicVars["Draw"].UpgradeValueBy(1)'),
        "BdMelter": ("new DamageVar(10", "CreatureCmd.LoseBlock", "Bd.Damage", "Damage.UpgradeValueBy(4)"),
        "BdRecycle": ("CardSelectCmd.FromHand", "ExhaustSelectionPrompt", "Bd.CostForEnergy(victim)", "CardCmd.Exhaust", "GainEnergy(energy)", "EnergyCost.UpgradeBy(-1)"),
        "BdReinforcedBody": ("HasEnergyCostX", "new BlockVar(7", "ResolveEnergyXValue", "Block.UpgradeValueBy(2)"),
        "BdReprogram": ('new DynamicVar("Focus", 1)', "new PowerVar<StrengthPower>(1)", "new PowerVar<DexterityPower>(1)", "-DynamicVars", "ApplyPower<StrengthPower>", "ApplyPower<DexterityPower>"),
        "BdSelfRepair": ("new HealVar(7)", "ApplyPower<BdSelfRepairPower>", "Heal.UpgradeValueBy(3)"),
        "BdStaticDischarge": ('new DynamicVar("Amount", 1)', "ApplyPower<BdStaticDischargePower>", 'DynamicVars["Amount"].UpgradeValueBy(1)'),
        "BdSeek": ('new DynamicVar("Amount", 1)', "CardKeyword.Exhaust", "base(0, CardType.Skill, CardRarity.Rare", "PileType.Draw.GetPile", "CardSelectCmd.FromSimpleGrid", "CardPileCmd.Add(card, PileType.Hand)", 'DynamicVars["Amount"].UpgradeValueBy(1)'),
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
    check(
        "Electrodynamics marks every Lightning passive through Triggered subscriptions",
        "orb.Triggered += MarkPassiveResolution" in electro_power
        and "AfterOrbChanneled" in electro_power
        and "AfterApplied" in electro_power,
    )
    check(
        "Electrodynamics spreads passive and evoke damage to missing opponents",
        "AfterDamageGiven" in electro_power
        and "AfterOrbEvoked" in electro_power
        and "SpreadToMissingOpponents" in electro_power
        and "Bd.Opponents(Owner)" in electro_power
        and "CreatureCmd.Damage" in electro_power,
    )
    check(
        "Electrodynamics spread has a recursion guard and no Lightning method detour",
        "IsSpreadingDamage" in electro_power
        and "BdElectrodynamicsLightningTargetPatch" not in cards
        and "TargetMethod()" not in electro_power
        and "static bool Prefix(" not in electro_power,
    )

    lock_on = class_body(cards, "BdLockOnPower")
    check("Lock-On returns multiplier 1.5 instead of multiplied damage", "return 1.5m;" in lock_on and "amount * 1.5m" not in lock_on)
    check("Lock-On expires by enemy turn instead of per hit", "AfterSideTurnEnd" in lock_on and "PowerCmd.TickDownDuration" in lock_on and "AfterDamageReceived" not in lock_on)

    power_specs = {
        "BdHeatsinksPower": ("AfterCardPlayed", "CardType.Power", "CardPileCmd.Draw"),
        "BdSelfRepairPower": ("AfterCombatEnd", "CreatureCmd.Heal"),
    }
    for class_name, tokens in power_specs.items():
        body = class_body(cards, class_name)
        missing = [token for token in tokens if token not in body]
        check(f"{class_name} effect route audit", not missing, f"missing {missing}")
    self_repair_power = class_body(cards, "BdSelfRepairPower")
    check(
        "Self Repair heals before combat powers are removed",
        "AfterCombatEnd" in self_repair_power
        and "override Task AfterCombatVictory" not in self_repair_power
        and "CreatureCmd.Heal(Owner, Amount)" in self_repair_power,
    )

    version_list_match = re.search(r"VersionedCardTypes\s*=\s*\[(?P<body>.*?)\];", versions, re.S)
    version_types = re.findall(r"typeof\((\w+)\)", version_list_match.group("body") if version_list_match else "")
    expected_version_types = [
        "Hotfix", "RocketPunch", "Voltaic", "Shatter", "TeslaCoil", "Uproar",
        "Fusion", "Compact", "MomentumStrike", "TrashToTreasure",
        "Barrage", "BeamCell", "ChargeBattery", "ColdSnap", "Coolheaded", "GoForTheEyes", "GunkUp", "Leap",
        "LightningRod", "SweepingBeam", "BdRecursion", "BdRecycle", "BdStreamline",
        "Chaos", "DoubleEnergy", "FightThrough", "Skim", "Tempest", "WhiteNoise",
        "Ftl", "Null", "Refract", "Feral", "Hailstorm", "Iteration", "Loop",
        "Smokestack", "Storm", "Subroutine", "BdReprogram", "BdStaticDischarge",
        "BulkUp", "HelixDrill", "BdReinforcedBody", "Synthesis", "Sunder",
        "BdMelter", "BdBullseye", "RipAndTear", "Scrape", "Hyperbeam", "Spinner",
        "AdaptiveStrike", "AllForOne", "BufferCard", "ConsumingShadow", "Coolant",
        "CreativeAi", "EchoForm", "FlakCannon", "GeneticAlgorithm", "IceLance",
        "Defragment", "BiasedCognition", "MeteorStrike", "MultiCast", "Rainbow",
        "BdThunderStrike", "BdCoreSurge",
    ]
    check("card-transformation registry contains exactly 69 cards", version_types == expected_version_types, repr(version_types))
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
        "Compact v0.99 Fuel draws cards without a synthetic DynamicVar": "ResolveFuelDrawCount(Fuel card)",
        "Scrape transformation uses all cost modifiers": "transformed ? CostModifiers.All : CostModifiers.Local",
        "Trash to Treasure v0.99 upgrade is Innate": "SetKeyword(card, CardKeyword.Innate, plus && upgradedVersion)",
        "Barrage custom route applies temporary Focus": "var temporaryFocus = card.DynamicVars.Damage.BaseValue",
        "Beam Cell custom route applies BetterDefect Lock-On": "Bd.ApplyPower<BdLockOnPower>",
        "Charge Battery custom route draws next turn": "Bd.ApplyPower<DrawCardsNextTurnPower>",
        "Cold Snap custom route channels two Frost": "await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);\n        await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);",
        "Coolheaded custom route draws two before channeling Frost": "PlayCoolheaded",
        "Go for the Eyes custom route always applies Weak": "PlayGoForTheEyes",
        "Gunk Up custom route generates Slimed into hand": "AddGeneratedCardToCombat(slimed, PileType.Hand",
        "Leap custom route becomes zero cost for combat": "card.EnergyCost.SetThisCombat(0)",
        "Lightning Rod custom route channels now and once next turn": "Bd.ApplyPower<LightningRodPower>",
        "Sweeping Beam custom normal upgrade draws two": 'SetDynamic(card, "Cards", upgradedVersion && plus ? 2m : 1m)',
        "Uproar custom route only selects highest-current-cost attacks": "c.Type == CardType.Attack",
        "Recycle transformed route exhausts a selected hand card": "private static async Task PlayRecycle(BdRecycle card",
        "Recycle transformed route gains one Orb slot": "await OrbCmd.AddSlots(card.Owner, 1);",
    }
    for name, token in behavior_checks.items():
        check(name, token in versions)
    common_play = class_body(versions, "BdCustomCommonCardPlayPatch")
    beam_start = common_play.find("private static async Task PlayBeamCell")
    beam_end = common_play.find("private static async Task PlayChargeBattery", beam_start)
    beam_route = common_play[beam_start:beam_end]
    check(
        "Beam Cell transformed route deals 3(4) damage before Lock-On",
        'SetDynamic(card, "Damage", plus ? 4m : 3m)' in versions
        and 'UpgradeDynamicTo(card, "Damage", 4m)' in versions
        and "DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)" in beam_route
        and beam_route.find("DamageCmd.Attack") < beam_route.find("Bd.ApplyPower<BdLockOnPower>"),
    )
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
    recycle_start = common_play.find("private static async Task PlayRecycle")
    recycle_end = common_play.find("private static async Task PlayChaos", recycle_start)
    recycle_route = common_play[recycle_start:recycle_end]
    check(
        "Recycle transformation is 1(0) cost, Exhaust, Common Skill",
        'case BdRecycle:' in versions
        and 'SetEnergy(card, plus ? 0 : 1);' in versions
        and 'SetKeyword(card, CardKeyword.Exhaust, true);' in versions
        and 'transformed ? CardRarity.Common : CardRarity.Uncommon' in versions
        and "base(1, CardType.Skill, CardRarity.Uncommon" in class_body(cards, "BdRecycle"),
    )
    check(
        "Recycle transformed effect replaces energy gain with one Orb slot",
        "CardSelectCmd.FromHand" in recycle_route
        and "CardCmd.Exhaust(choiceContext, victim)" in recycle_route
        and "OrbCmd.AddSlots(card.Owner, 1)" in recycle_route
        and "GainEnergy" not in recycle_route,
    )
    uncommon_behavior_checks = {
        "Chaos prioritizes missing orb types": "missing.Count > 0 ? missing : canonical",
        "Chaos custom baseline channels two": 'SetDynamic(card, "Repeat", upgradedVersion ? 2m',
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
        "Hailstorm creates one damage event per Frost orb": "for (var frost = 0; frost < frostCount; frost++)",
        "Iteration defers status exhaustion until the draw command completes": "BdCustomIterationDrawCompletionPatch",
        "Loop triggers both edge orbs": "BdCustomLoopPowerPatch",
        "Smokestack draws on its first trigger": "BdCustomSmokestackPowerPatch",
        "Storm gains Innate when transformed": "SetKeyword(card, CardKeyword.Innate, upgradedVersion)",
        "Subroutine draws on its first trigger": "BdCustomSubroutinePowerPatch",
        "Reprogram removes or evokes every orb before applying stats": "var orbCount = Owner.PlayerCombatState.OrbQueue.Orbs.Count",
        "Static Discharge transformed route grants three Block": "GainBlock(Owner, 3m, ValueProp.Unpowered, null)",
        "Bulk Up tracks later orb-slot losses": "BdBulkUpPower.NotifySlotsLost",
        "Helix Drill doubles final X of at least four": "if (x >= 4) x *= 2;",
        "Reinforced Body doubles final X of at least four": "BdCardVersionUpgrades.IsVersionEnabled(this) && x >= 4",
        "Synthesis draws a Power then makes the next Power free": "private static async Task PlaySynthesis",
        "Sunder discounts itself when it does not kill": "card.EnergyCost.AddThisCombat(-1, reduceOnly: true)",
        "Melter transformed route applies Vulnerable": "Bd.ApplyPower<VulnerablePower>",
        "Bullseye marks the priority target": "BdBullseyeTargetPower",
        "Rip and Tear performs three random hits and a repeat bonus": "private static async Task PlayRipAndTear",
        "Scrape grants temporary Strength for retained cards": "BdScrapeTemporaryStrengthPower",
        "Hyperbeam loses Focus per current orb": "var orbCount = card.Owner.PlayerCombatState.OrbQueue.Orbs.Count",
        "Spinner preserves Glass passive values": "current + 1m",
    }
    for name, token in uncommon_behavior_checks.items():
        check(name, token in versions or token in cards)

    feral_result_patch = class_body(versions, "BdCustomFeralPowerResultPatch")
    feral_power_vfx_patch = class_body(versions, "BdCustomFeralPowerFlyVfxPatch")
    check(
        "Feral tracks a returned card until its play lifecycle finishes",
        "ConditionalWeakTable<CardModel, ReturnMarker>" in feral_result_patch
        and "MarkReturningCard(card)" in feral_result_patch
        and 'typeof(CardModel).GetEvent(' in feral_result_patch
        and '"Played"' in feral_result_patch,
    )
    check(
        "Feral returning Power animates a clone and preserves the real hand card node",
        'AccessTools.DeclaredMethod(typeof(CardModel), "PlayPowerCardFlyVfx")' in feral_power_vfx_patch
        and "NCard.FindOnTable(card)" in feral_power_vfx_patch
        and "card.ClonePreservingMutability()" in feral_power_vfx_patch
        and "NCard.Create(visualCard)" in feral_power_vfx_patch
        and "NCardFlyPowerVfx.Create(clone)" in feral_power_vfx_patch
        and "TaskHelper.RunSafely(flyVfx.PlayAnim())" in feral_power_vfx_patch
        and "original.QueueFree" not in feral_power_vfx_patch,
    )
    check(
        "Chaos complete random pool explicitly includes Glass",
        "ModelDb.Orb<GlassOrb>()" in versions
        and "ModelDb.Orbs.ToList()" not in versions,
    )
    check(
        "Chaos transformed description explicitly includes Glass",
        "随机充能球（包括玻璃）" in localization,
    )
    iteration_draw_patch = class_body(versions, "BdCustomIterationDrawCompletionPatch")
    check(
        "Iteration never moves the status card during AfterCardDrawn",
        "FinishDrawAndExhaust" in iteration_draw_patch
        and "var drawnCards = (await original).ToList();" in iteration_draw_patch
        and "firstStatus.Pile?.Type == PileType.Hand" in iteration_draw_patch
        and "CardCmd.Exhaust(choiceContext, firstStatus)" in iteration_draw_patch
        and "BdCustomIterationPowerPatch" not in versions
        and "FinishAndExhaust(__result" not in versions,
    )
    check(
        "Chaos custom baseline Exhausts until upgraded",
        'SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus)' in versions,
    )
    check(
        "Chaos ID fallback upgrade removes Exhaust",
        'case "CARD.CHAOS":\n                UpgradeDynamicTo(card, "Repeat", 2m);\n                SetKeyword(card, CardKeyword.Exhaust, false);'
        in versions,
    )
    check(
        "Chaos encyclopedia summary says two orbs and Exhaust removal",
        '["CARD.CHAOS"] = ("改造：自定义", "1费生成2个随机充能球（包括玻璃），优先生成当前栏位中没有的种类；基础牌消耗，普通升级移除消耗")'
        in versions,
    )
    coolheaded_route = re.search(
        r"private static async Task PlayCoolheaded\(.*?\)\s*\{(?P<body>.*?)\n    \}",
        versions,
        re.S,
    )
    coolheaded_body = coolheaded_route.group("body") if coolheaded_route else ""
    check(
        "Coolheaded custom route draws before channeling Frost",
        coolheaded_body.find("CardPileCmd.Draw") >= 0
        and coolheaded_body.find("OrbCmd.Channel<FrostOrb>") > coolheaded_body.find("CardPileCmd.Draw"),
    )
    check(
        "Coolheaded transformed values draw two and Exhaust until upgraded",
        'SetDynamic(card, "Cards", upgradedVersion ? 2m' in versions
        and 'SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus)' in versions,
    )
    rare_behavior_checks = {
        "Adaptive Strike adds an Ethereal zero-cost copy to draw pile": "clone.AddKeyword(CardKeyword.Ethereal)",
        "All for One selects up to two or three zero-cost discard cards": "CardSelectCmd.FromSimpleGrid",
        "Buffer also grants ten block": "GainBlock(card.Owner.Creature, 10m",
        "Consuming Shadow triggers every Dark passive": "Orbs.OfType<DarkOrb>()",
        "Coolant transformed route runs at turn end": "BdCustomCoolantEndPatch",
        "Creative AI offers three power choices": "BdCustomCreativeAiPowerPatch",
        "Echo Form duplicates the second card": "if (priorPlays == 1)",
        "Flak Cannon targets one chosen enemy using exhaust-pile count": "var hitCount = PileType.Exhaust.GetPile(card.Owner).Cards.Count",
        "Meteor Strike channels exactly two Plasma": "PlayMeteorStrike",
        "Multi-Cast recreates each double-evoked rightmost orb type": "ChannelSameType",
        "Rainbow channels all five requested orb types": "PlayRainbow",
        "rare rarity transformations are routed": "TryGetTransformedRarity",
        "rarity-changing cards migrate persisted odds": "MoveWeightToTransformedRarity",
    }
    for name, token in rare_behavior_checks.items():
        check(name, token in versions)
    multi_cast_play = re.search(
        r"internal static async Task PlayMultiCast\(.*?(?=\n    private static async Task ChannelSameType)",
        versions,
        re.S,
    )
    multi_cast_body = multi_cast_play.group(0) if multi_cast_play else ""
    check(
        "transformed Multi-Cast repeats X or X+1 rightmost double-evokes",
        "card.ResolveEnergyXValue() + (card.IsUpgraded ? 1 : 0)" in multi_cast_body
        and "OrbQueue.Orbs.FirstOrDefault()" in multi_cast_body
        and "OrbCmd.EvokeNext(choiceContext, card.Owner, dequeue: false)" in multi_cast_body
        and "OrbCmd.EvokeNext(choiceContext, card.Owner);" in multi_cast_body
        and "ChannelSameType(" in multi_cast_body,
    )
    check(
        "transformed Multi-Cast preserves accumulated Dark evoke damage",
        'AccessTools.Field(typeof(DarkOrb), "_evokeVal")' in versions
        and "rightmost is DarkOrb ? rightmost.EvokeVal : (decimal?)null" in multi_cast_body
        and "MultiCastDarkEvokeValField.SetValue(" in versions
        and "OrbCmd.Channel(choiceContext, replacement, player)" in versions,
    )
    check(
        "transformed Multi-Cast description matches rightmost double-evoke loop",
        '重复{IfUpgraded:show:X+1|X}次：[gold]激发[/gold]最右侧充能球2次' in localization,
    )
    check(
        "transformed Consuming Shadow upgrades from one to two Dark orbs",
        'SetDynamic(card, "Repeat", upgradedVersion ? plus ? 2m : 1m' in versions
        and 'case ConsumingShadow:\n                UpgradeDynamicTo(card, "Repeat", upgradedVersion ? 2m : 3m);' in versions
        and 'case "CARD.CONSUMING_SHADOW":\n                SetEnergy(card, 2);\n                SetDynamic(card, "Repeat", upgradedVersion ? plus ? 2m : 1m' in versions
        and 'case "CARD.CONSUMING_SHADOW": UpgradeDynamicTo(card, "Repeat", upgradedVersion ? 2m : 3m);' in versions
        and 'ConsumingShadow => "生成1(2)个黑暗' in versions,
    )
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
    loop_patch = class_body(versions, "BdCustomLoopPowerPatch")
    check(
        "Loop transformed edge passives scale with stacked copies",
        "repeat < power.Amount" in loop_patch
        and "OrbCmd.Passive(choiceContext, orbs[0], null)" in loop_patch
        and "OrbCmd.Passive(choiceContext, orbs[^1], null)" in loop_patch,
    )
    check(
        "Loop treats one orb as both the leftmost and rightmost orb",
        "orbs.Count > 1" not in loop_patch
        and "player.PlayerCombatState.OrbQueue.Orbs.Contains(orbs[^1])" not in loop_patch
        and "await OrbCmd.Passive(choiceContext, orbs[^1], null);" in loop_patch,
    )
    check(
        "Smokestack transformed first-trigger draw scales by applied copies",
        "GetStackCount(power)" in smokestack_patch
        and "OfType<PowerReceivedEntry>()" in smokestack_patch
        and "entry.Amount > 0" in smokestack_patch,
    )
    check(
        "Subroutine transformed first-trigger draw uses pre-play stack amount",
        "GetTrackedStackAmount(__instance, cardPlay.Card)" in subroutine_patch
        and "dictionary[card] is int amount" in subroutine_patch
        and "FinishAndDraw(" in subroutine_patch
        and "__state);" in subroutine_patch,
    )
    recursion_body = class_body(cards, "BdRecursion")
    check(
        "Recursion custom route resolves the visual leftmost orb from the queue tail",
        "Orbs.LastOrDefault()" in recursion_body
        and "Orbs.FirstOrDefault()" in recursion_body
        and "var transformed =" in recursion_body,
    )
    check(
        "Recursion custom route double-evokes the selected leftmost orb",
        "OrbCmd.EvokeLast(choiceContext, Owner, dequeue: false)" in recursion_body
        and "OrbCmd.EvokeLast(choiceContext, Owner);" in recursion_body
        and "var t = orb.GetType();" in recursion_body,
    )
    check(
        "Recursion preserves accumulated Dark orb evoke damage",
        'AccessTools.Field(typeof(DarkOrb), "_evokeVal")' in recursion_body
        and "var inheritedDarkEvokeVal = orb is DarkOrb ? orb.EvokeVal : (decimal?)null;" in recursion_body
        and "var replacement = ModelDb.Orb<DarkOrb>().ToMutable();" in recursion_body
        and "DarkEvokeValField.SetValue(replacement, inheritedDarkEvokeVal!.Value);" in recursion_body
        and "OrbCmd.Channel(choiceContext, replacement, Owner)" in recursion_body
        and "OrbCmd.Channel<DarkOrb>" not in recursion_body,
    )
    check(
        "Streamline custom route uses 15 and 20 damage",
        "upgradedVersion ? plus ? 20m : 15m" in versions
        and 'UpgradeDynamicTo(card, "Damage", 20m)' in versions
        and 'SetDynamic(card, "Damage", upgradedVersion ? plus ? 20m : 15m : plus ? 20m : 15m);' in versions,
    )
    check("Streamline custom route discounts every copy", "AllCards.OfType<BdStreamline>()" in cards)

    check("Recycle localization describes selection", "选择并[gold]消耗[/gold] 1 张手牌" in localization)
    check("Electrodynamics localization covers passive and evoke", "被动与激发伤害会命中所有敌人" in localization)
    check("Fission description switches remove/evoke with normal upgrade", "{IfUpgraded:show:[gold]激发[/gold]所有充能球。|移除所有充能球。}" in localization)
    check("Core Surge and Fission rely on the real Exhaust keyword text", '["cards/BD_CORE_SURGE.description"]' in localization and '["cards/BD_FISSION.description"]' in localization and "\\n[gold]消耗[/gold]。" not in localization)
    check(
        "transformed Innate cards rely on the real Innate keyword text",
        '"[gold]固有[/gold]。\\n每当你打出一张能力牌时' not in localization
        and '"{IfUpgraded:show:[gold]固有[/gold]。\\n|}每当你受到' not in localization
        and "SetKeyword(card, CardKeyword.Innate" in versions,
    )
    check("Rocket Punch description follows its historical behavior switch", 'rocketV100' in localization and "直到打出或当前回合结束" in localization)
    check("Tesla Coil description switches transformed passive count and keeps dynamic damage", 'teslaV105' in localization and "造成{Damage:diff()}点伤害" in localization and "充能球被动{IfUpgraded:show:两次|一次}" in localization)
    check("Shatter description explicitly says every orb is evoked twice", '["SHATTER.description"]' in localization and "[gold]激发[/gold]所有充能球两次" in localization)
    check(
        "Fuel description hides drawing when Compact uses v0.108 behavior and uses only native variables",
        'compactV099' in localization
        and '["FUEL.description"]' in localization
        and "抽{IfUpgraded:show:2|1}张牌" in localization
        and "{Cards:diff()}" not in localization[localization.index('["FUEL.description"]'):localization.index('["SCRAPE.description"]')],
    )
    fuel_play = class_body(versions, "BdCardVersionFuelPlayPatch")
    check(
        "Fuel play never indexes the absent v110.1 Cards dynamic variable",
        "ResolveFuelDrawCount(card)" in fuel_play
        and 'DynamicVars["Cards"]' not in fuel_play,
    )
    check(
        "Scrape description matches retained-card temporary Strength",
        "scrapeCustom" in localization
        and "每保留1张牌，本回合获得1点[gold]临时力量[/gold]" in localization
        and "按卡牌自身耗能计算" in localization,
    )
    check("custom transformations are labelled exactly", '"改造：自定义"' in versions and 'targetLabel.StartsWith("改造："' in read("BetterDefectCode/CardUpgradeUi.cs"))
    check("custom common-card descriptions follow their switches", all(token in localization for token in (
        "barrageCustom", "beamCellCustom", "chargeBatteryCustom", "coldSnapCustom", "coolheadedCustom", "goForTheEyesCustom",
        "gunkUpCustom", "leapCustom", "lightningRodCustom", "sweepingBeamCustom", "uproarCustom",
        "recursionCustom", "streamlineCustom",
    )))
    check(
        "Barrage transformed description says temporary Focus and one passive trigger",
        "获得{Damage:diff()}点[gold]临时集中[/gold]，然后触发你的所有充能球的被动一次。" in localization,
    )
    check(
        "Beam Cell transformed description includes damage and Lock-On",
        '"造成{Damage:diff()}点伤害。\\n给予{VulnerablePower:diff()}层[gold]锁定[/gold]。"' in localization,
    )
    check("custom uncommon-card descriptions follow their switches", all(token in localization for token in (
        "chaosCustom", "doubleEnergyCustom", "fightThroughCustom", "skimCustom", "tempestCustom",
        "whiteNoiseCustom", "ftlCustom", "nullCustom", "refractCustom", "feralCustom",
        "hailstormCustom", "iterationCustom", "loopCustom", "smokestackCustom", "stormCustom",
        "subroutineCustom",
    )))
    check("Feral custom text includes every zero-energy card type", '? "你每回合第一次打出的耗能为0' in localization and '的牌，会放回你的[gold]手牌[/gold]' in localization)
    transformed_power_text = {
        "Feral power bar includes every zero-energy card type": (
            '"FERAL_POWER.smartDescription"' in localization
            and "次打出0{energyPrefix:energyIcons(1)}牌时" in localization
        ),
        "Hailstorm power bar describes per-Frost damage": (
            '"HAILSTORM_POWER.smartDescription"' in localization
            and "每有1个[gold]冰霜[/gold]充能球，就分别对所有敌人造成[blue]{Amount}[/blue]点伤害" in localization
        ),
        "Iteration power bar includes status exhaustion": (
            '"ITERATION_POWER.smartDescription"' in localization
            and "然后[gold]消耗[/gold]该状态牌" in localization
        ),
        "Loop power bar includes both edge orbs": (
            '"LOOP_POWER.smartDescription"' in localization
            and "分别触发最左侧和最右侧充能球" in localization
            and "同一个充能球" not in localization
        ),
        "Smokestack power bar includes first-trigger draw": (
            '"SMOKESTACK_POWER.smartDescription"' in localization
            and "每层在每回合第一次触发时，额外抽1张牌" in localization
        ),
        "Subroutine power bar includes first-trigger draw": (
            '"SUBROUTINE_POWER.smartDescription"' in localization
            and "每层在每回合第一次触发时，额外抽1张牌" in localization
        ),
    }
    for name, condition in transformed_power_text.items():
        check(name, condition)
    check("Seek selects one or two draw-pile cards and exhausts", "从你的抽牌堆中选择 {Amount:diff()} 张牌放入手牌" in localization and "CardKeyword.Exhaust" in class_body(cards, "BdSeek"))
    check("Reprogram+ keeps Focus loss at one", 'DynamicVars["Focus"].UpgradeValueBy' not in class_body(cards, "BdReprogram") and 'DynamicVars.Strength.UpgradeValueBy(1)' in class_body(cards, "BdReprogram") and 'DynamicVars.Dexterity.UpgradeValueBy(1)' in class_body(cards, "BdReprogram"))
    ui = read("BetterDefectCode/CardUpgradeUi.cs")
    helper = (ROOT / "tools" / "prepare_v103_source.py").read_text(encoding="utf-8")
    check("Android skips unsafe NCard.Model setter detour", "DisableUnsafeAndroidSetterDetour = false" in ui and "DisableUnsafeAndroidSetterDetour = true" in helper)
    assign_patch = class_body(ui, "BdCardUpgradeLibraryGridAssignPatch")
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
    hud = read("BetterDefectCode/CardUpgradeStatsHud.cs")
    visibility_patch = class_body(ui, "BdCardUpgradeLibraryVisibilityPatch")
    check(
        "HUD follows NSubmenu visibility transitions",
        'HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NSubmenu), "OnScreenVisibilityChange")' in ui
        and "__instance is NCardLibrary library" in visibility_patch
        and "SyncLibraryVisibility(library)" in visibility_patch,
    )
    check(
        "HUD is bound to the exact visible card library",
        "private static NCardLibrary? _activeLibrary;" in hud
        and "var grid = BdCardUpgradeUi.GetLibraryGrid(library);" in hud
        and "BdCardUpgradeUi.IsCardLibraryContext(grid)" in hud
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
        "BdCardUpgradeUi.CleanupAllTouchedCards();" in hud
        and "if (_wasVisible || _library is not null)" in hud
        and "BdCardUpgradeUi.ApplyLibraryGrid(grid);" in hud,
    )
    for power_id in (
        "BD_HEATSINKS_POWER", "BD_SELF_REPAIR_POWER", "BD_STATIC_DISCHARGE_POWER",
        "BD_ELECTRODYNAMICS_POWER", "BD_LOCK_ON_POWER",
    ):
        check(f"{power_id} has a smart combat description", f'power/{power_id}.smartDescription' in localization or f'powers/{power_id}.smartDescription' in localization)
    for power_type in (
        "BdHeatsinksPower", "BdSelfRepairPower", "BdStaticDischargePower",
        "BdElectrodynamicsPower", "BdLockOnPower",
    ):
        check(f"{power_type} redirects its missing power icon", f"typeof({power_type})" in power_icons)
    check("power icon redirect patches status and large icons", "PackedIconPath" in power_icons and "BigIconPath" in power_icons)
    check("power icon redirect validates bundled resources", "ResourceLoader.Exists(candidate)" in power_icons)
    check("Android patches final power texture getter", '[HarmonyPatch(typeof(PowerModel), "get_Icon")]' in power_icons)
    check("injected powers validate all five status textures", "ValidateInjectedStatusIcons" in power_icons and "BdPowerIconPathPatch.ValidateInjectedStatusIcons();" in read("BetterDefectCode/Patches.cs"))
    check("Android power-icon detour replaces beta portrait detour", "type == typeof(BdPowerIconPathPatch)" in read("BetterDefectCode/MainFile.cs") and "type == typeof(BetterDefectBetaPortraitPatch)" in read("BetterDefectCode/MainFile.cs"))
    main_file = read("BetterDefectCode/MainFile.cs")
    patches = read("BetterDefectCode/Patches.cs")
    check(
        "Android skips redundant unlocked-card-pool detour",
        "type == typeof(DefectCardPoolUnlockedCardsPatch)" in main_file
        and "GenerateAllCards remains extended" in main_file,
    )
    check(
        "Android skips two non-gameplay tooltip detours",
        "type == typeof(BdCustomBeamCellHoverTipsPatch)" in main_file
        and "type == typeof(BdCustomFightThroughHoverTipsPatch)" in main_file,
    )
    check(
        "rare-card Android play hooks are independently patchable",
        "class BdCustomRareAdaptiveStrikePlayPatch" in versions
        and "class BdCustomRareAllForOnePlayPatch" in versions
        and "class BdCustomRareBufferPlayPatch" in versions
        and "class BdCustomRareFlakCannonPlayPatch" in versions
        and "class BdCustomRareMeteorStrikePlayPatch" in versions
        and "class BdCustomRareMultiCastPlayPatch" in versions
        and "class BdCustomRareRainbowPlayPatch" in versions,
    )
    check(
        "obsolete grouped rare-card Harmony patch is removed",
        "[HarmonyPatch]\ninternal static class BdCustomRareCardPlayPatch" not in versions,
    )
    hud = read("BetterDefectCode/CardUpgradeStatsHud.cs")
    upgrade_state = read("BetterDefectCode/CardUpgradeState.cs")
    check(
        "card-point budget is 50 with 25 Normal and 10 Overclock points",
        "MaxCardPointBudget = 50" in upgrade_state
        and "NormalPointLimit = 25" in upgrade_state
        and "OverclockPointLimit = 35" in upgrade_state,
    )
    check(
        "point HUD has blue yellow and red segment tiers",
        "new StyleBoxFlat[2, 3]" in hud
        and "i < BlueLimit ? 0 : i < YellowLimit ? 1 : 2" in hud
        and "NormalOverclockGap" in hud
        and "OverclockOverloadGap" in hud,
    )
    check(
        "point HUD labels Normal Overclock and Overload stages",
        '0 => "正常"' in hud
        and '1 => "超频"' in hud
        and '_ => "过载"' in hud,
    )
    check(
        "upgrade tooltip explains the 50-point three-stage budget",
        "共享50点上限：25点正常、10点超频、15点过载" in ui,
    )
    check("removed Amplify state is purged from persistent point usage", 'RemovedAmplifyId = "CARD.BD_AMPLIFY"' in upgrade_state and "UpgradedCards.RemoveAll" in upgrade_state)
    check("Recycle transformed localization matches its effect", '"BD_RECYCLE.description"' in localization and "获得1个[gold]充能球栏位[/gold]" in localization)
    check(
        "rarity migration uses explicit before and after transformation states",
        "GetRarityForVersionState(card, wasUpgraded)" in versions
        and "GetRarityForVersionState(card, !wasUpgraded)" in versions,
    )
    check("manifest is v0.11.42", '"version": "0.11.42"' in manifest)
    check(
        "Darv Dusty Tome compatibility preserves transformed Biased Cognition",
        "class BdDustyTomeAncientCardCompatibilityPatch" in patches
        and "typeof(DustyTome)" in patches
        and "nameof(DustyTome.SetupForPlayer)" in patches
        and "card is BiasedCognition" in patches
        and "__instance.AncientCard = fallback.Id;" in patches
        and "return false;" in patches,
    )
    check(
        "Dusty Tome compatibility defers to vanilla when an Ancient candidate exists",
        "card.Rarity == CardRarity.Ancient" in patches
        and "!ArchaicTooth.TranscendenceCards.Contains(card)" in patches
        and "return true;" in patches,
    )
    check(
        "Android startup keeps new lifecycle behavior inside the stable patch-class budget",
        "class BdCardVersionPersistedStatePatch" in versions
        and "class BdCardVersionRunReadyPatch" not in versions
        and "class BdCardVersionPlayerSyncPatch" not in versions
        and "class BdCardVersionDowngradePatch" not in versions,
    )
    check(
        "Android rarity transformations avoid native CardModel getter detours",
        "type == typeof(OldDefectCardPoolPatch)" in main_file
        and "type == typeof(OldDefectCardRarityPatch)" in main_file
        and '"<Rarity>k__BackingField"' in versions
        and "ApplyAndroidRarityWithoutDetour(card, upgradedVersion);" in versions,
    )
    check(
        "hidden v103 cards normalize Event rarity without Android detours",
        '"<Rarity>k__BackingField"' in old_cards
        and "NormalizeRestoredRarity(card)" in old_cards
        and "CardRarityBackingField.SetValue(card, rarity);" in old_cards
        and "normalizedV103Rarities={normalizedRarities}/{Rarities.Count}" in old_cards
        and "[typeof(HelloWorld)] = CardRarity.Uncommon" in old_cards
        and "[typeof(Rebound)] = CardRarity.Common" in old_cards
        and "[typeof(RipAndTear)] = CardRarity.Uncommon" in old_cards
        and "[typeof(Stack)] = CardRarity.Common" in old_cards,
    )
    check(
        "card transformation normalization reapplies enchantment last",
        "ReapplyEnchantmentAsFinalModifier(card);" in versions
        and "card.Enchantment.ModifyCard();" in versions,
    )
    check(
        "enchantment refresh is limited to eligible transformed cards",
        "if (!IsEligible(card) && card is not Fuel) return;" in versions
        and "if (!card.IsMutable || card.Enchantment == null) return;" in versions,
    )
    check(
        "persisted transformations are reapplied to every loaded player pile",
        "ReapplyPersistedTransformationsToLoadedCards" in versions
        and "foreach (var player in state.Players)" in versions
        and "foreach (var pile in player.Piles)" in versions
        and "ApplyToModel(card);" in versions,
    )
    check(
        "merged persisted-state patch restores transformed values after full deserialization",
        "class BdCardVersionPersistedStatePatch" in versions
        and "AccessTools.Method(typeof(NRun), nameof(NRun._Ready))" in versions
        and '? "run ready"' in versions
        and "ReapplyPersistedTransformationsToLoadedCards(source);" in versions,
    )
    check(
        "merged persisted-state patch restores transformed values after run synchronization",
        "class BdCardVersionPersistedStatePatch" in versions
        and "AccessTools.Method(typeof(Player), nameof(Player.SyncWithSerializedPlayer))" in versions
        and '"player sync"' in versions,
    )
    check(
        "normal upgrade and downgrade share one Android-safe patch class",
        "class BdCardVersionNormalUpgradePatch" in versions
        and "nameof(CardModel.UpgradeInternal)" in versions
        and "nameof(CardModel.DowngradeInternal)" in versions
        and "class BdCardVersionDowngradePatch" not in versions,
    )
    check(
        "Android delayed patch completion refreshes canonical and loaded cards",
        "OldDefectCards.RefreshAfterDeferredPatchInstall();" in main_file
        and "BdLocalization.MergeIntoLocManager();" in main_file
        and "BdCardVersionUpgrades.RefreshAllCanonicalModels();" in main_file
        and 'ReapplyPersistedTransformationsToLoadedCards("Android patch queue completion")' in main_file,
    )
    check(
        "Android delayed patch completion merges restored-card localization after LocManager initialization",
        "LocManager.Initialize has also already finished" in main_file
        and main_file.index("OldDefectCards.RefreshAfterDeferredPatchInstall();")
        < main_file.index("BdLocalization.MergeIntoLocManager();", main_file.index("OldDefectCards.RefreshAfterDeferredPatchInstall();"))
        < main_file.index("BdCardVersionUpgrades.RefreshAllCanonicalModels();"),
    )
    check(
        "Android delayed patch completion rebuilds and rebinds the cached Defect pool",
        "RefreshAfterDeferredPatchInstall" in old_cards
        and "EnsureInjected(resetGlobalCards: false);" in old_cards
        and "var rebuilt = pool.AllCards.ToArray();" in old_cards
        and 'AccessTools.Field(typeof(CardPoolModel), "_allCards")?.SetValue(pool, rebuilt);' in old_cards
        and "cardPoolField?.SetValue(card, pool)" in old_cards
        and 'Field(typeof(ModelDb), "_allCards")?.SetValue(null, globalCards)' in old_cards
        and "globalRestored=" in old_cards
        and "globalDefect=" in old_cards,
    )
    check(
        "visible Android encyclopedia replaces and rarity-sorts its stale pre-patch card snapshot",
        "RefreshCardLibraryGridIfStale" in old_cards
        and 'AccessTools.Field(typeof(NCardLibraryGrid), "_allCards")' in old_cards
        and "class CardLibraryInitialComparer" in old_cards
        and "var rarityOrder = x.Rarity.CompareTo(y.Rarity);" in old_cards
        and "canonical.Sort(new CardLibraryInitialComparer(ModelDb.AllCardPools.ToList()));" in old_cards
        and "SequenceEqual(canonical.Select(SafeCardId), StringComparer.Ordinal)" in old_cards
        and "encyclopedia card ordering verified:" in old_cards
        and "rarityOrderRepaired={!orderMatches}" in old_cards
        and "grid.RefreshVisibility();" in old_cards
        and "OldDefectCards.RefreshCardLibraryGridIfStale(grid)" in ui
        and 'AccessTools.Method(typeof(NCardLibrary), "UpdateFilter")' in ui
        and "LibraryUpdateFilterMethod?.Invoke(library, [false])" in ui,
    )
    check(
        "Iteration waits one process frame and removes stale hand visuals",
        "WaitForIterationVisualCleanupFrame" in versions
        and "SceneTree.SignalName.ProcessFrame" in versions
        and "CleanupIterationHandVisuals" in versions
        and "hand.RemoveCardHolder(holder)" in versions,
    )
    android_dispatch = read("BetterDefectCode/AndroidCentralCardPlay.cs")
    check("Android bridge handler returns null for native card dispatch", "internal static Task? TryOnPlay" in android_dispatch and "return null;" in android_dispatch)
    check("Android bridge registration stores MethodInfo without a generic Func delegate", "handlerField.SetValue(null, dispatcher)" in main_file and "Delegate.CreateDelegate" not in main_file)
    check("encyclopedia context is owned by the current scene", "IsUnderCurrentScene(library)" in ui)
    check("full pooled-card cleanup exists", "internal static void CleanupAllTouchedCards()" in ui)
    check("library watcher synchronously strips pooled controls", "CleanupAllTouchedCards();" in hud and "_library = null;" in hud)
    check("cross-version combat state uses reflection", 'AccessTools.Property(sourceType, "CombatState")' in cards)
    check("cross-version enemy targeting avoids direct CombatState typing", "TryTargetAllOpponents(object attackCommand, CardModel card)" in cards)
    check("Electrodynamics uses cross-version opponent lookup", "Bd.Opponents(Owner)" in electro_power)
    check("source contains no direct model CombatState access", "card.CombatState" not in cards and "orb.CombatState" not in cards)
    check("Shatter uses cross-version all-opponent targeting", "Cards.Bd.TryTargetAllOpponents(attack, card)" in versions)
    check("Shatter no longer directly targets card.CombatState", ".TargetingAllOpponents(card.CombatState)" not in versions)

    for binary in args.binary:
        exists = binary.is_file() and binary.stat().st_size > 100_000
        check(f"compiled binary exists: {binary}", exists)
    for binary in args.mobile_binary:
        exists = binary.is_file() and binary.stat().st_size > 100_000
        check(f"compiled mobile binary exists: {binary}", exists)
        if exists:
            data = binary.read_bytes()
            check(
                f"mobile binary has no PC-only ICombatState metadata: {binary}",
                b"ICombatState" not in data,
            )

    lines = [
        "BetterDefect v0.11.39 offline audit",
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
