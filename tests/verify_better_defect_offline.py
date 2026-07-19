#!/usr/bin/env python3
"""Offline structural regression checks for BetterDefect.

These checks deliberately do not start Slay the Spire 2.  They verify the
restored-card registry, every recreated StS1 card's defining values/behavior,
the four audited power fixes, and the 14 historical-version upgrade routes.
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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", type=Path)
    parser.add_argument("--binary", type=Path, action="append", default=[])
    args = parser.parse_args()

    cards = (PROJECT / "BetterDefectCode" / "CardsAndPowers.cs").read_text(encoding="utf-8")
    old_cards = (PROJECT / "BetterDefectCode" / "OldDefectCards.cs").read_text(encoding="utf-8")
    versions = (PROJECT / "BetterDefectCode" / "CardVersionUpgrades.cs").read_text(encoding="utf-8")
    localization = (PROJECT / "BetterDefectCode" / "Localization.cs").read_text(encoding="utf-8")
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
    check("Electrodynamics patches Lightning passive and evoke common path", "ApplyLightningDamage" in electro_patch and "GetOpponentsOf" in electro_patch and "CreatureCmd.Damage" in electro_patch)

    lock_on = class_body(cards, "BdLockOnPower")
    check("Lock-On returns multiplier 1.5 instead of multiplied damage", "return 1.5m;" in lock_on and "amount * 1.5m" not in lock_on)
    check("Lock-On expires by enemy turn instead of per hit", "AfterSideTurnEnd" in lock_on and "PowerCmd.TickDownDuration" in lock_on and "AfterDamageReceived" not in lock_on)

    power_specs = {
        "BdHeatsinksPower": ("AfterCardPlayed", "CardType.Power", "CardPileCmd.Draw"),
        "BdSelfRepairPower": ("AfterCombatVictory", "CreatureCmd.Heal"),
        "BdAmplifyPower": ("ModifyCardPlayCount", "playCount + 1", "AfterCardPlayed", "-1"),
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
    ]
    check("historical-version registry contains exactly 14 cards", version_types == expected_version_types, repr(version_types))
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
    }
    for name, token in behavior_checks.items():
        check(name, token in versions)

    check("Recycle localization describes selection", "选择并[gold]消耗[/gold] 1 张手牌" in localization)
    check("Electrodynamics localization covers passive and evoke", "被动与激发伤害会命中所有敌人" in localization)
    check("manifest is v0.8.0", '"version":  "0.8.0"' in manifest)

    for binary in args.binary:
        exists = binary.is_file() and binary.stat().st_size > 100_000
        check(f"compiled binary exists: {binary}", exists)

    lines = [
        "BetterDefect v0.8.0 offline audit",
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
