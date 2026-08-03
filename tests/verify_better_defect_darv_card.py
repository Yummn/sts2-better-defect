#!/usr/bin/env python3
"""Focused offline regression audit for Darv's reworked Biased Cognition."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT if (ROOT / "BetterDefectCode").is_dir() else ROOT / "src"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def class_body(source: str, class_name: str) -> str:
    match = re.search(rf"\bclass\s+{re.escape(class_name)}\b", source)
    if not match:
        raise AssertionError(f"class not found: {class_name}")
    start = source.find("{", match.end())
    depth = 0
    for index in range(start, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[start + 1:index]
    raise AssertionError(f"unclosed class: {class_name}")


def audit_source(source_root: Path, label: str, check: callable) -> None:
    code = source_root / "BetterDefectCode"
    cards = read(code / "CardsAndPowers.cs")
    registry = read(code / "OldDefectCards.cs")
    patches = read(code / "Patches.cs")
    localization = read(code / "Localization.cs")
    icons = read(code / "PowerIcons.cs")
    manifest = json.loads(read(source_root / "BetterDefect.json"))

    card = class_body(cards, "BdReworkedBiasedCognition")
    power = class_body(cards, "BdReworkedBiasedCognitionPower")
    darv = class_body(patches, "BdDustyTomeAncientCardCompatibilityPatch")

    check(f"{label}: manifest v0.11.36", manifest.get("version") == "0.11.36")
    check(f"{label}: card is 1-cost Ancient power", "base(1, CardType.Power, CardRarity.Ancient, TargetType.Self)" in card)
    check(f"{label}: base Focus is 4 and upgrade adds 1", "new PowerVar<FocusPower>(4)" in card and 'DynamicVars["FocusPower"].UpgradeValueBy(1)' in card)
    check(f"{label}: upkeep is 2 Focus", 'new DynamicVar("Decay", 2)' in card and 'DynamicVars["Decay"].BaseValue' in card)
    check(f"{label}: card applies Focus and custom upkeep power", "Bd.ApplyPower<FocusPower>" in card and "Bd.ApplyPower<BdReworkedBiasedCognitionPower>" in card)
    check(f"{label}: card remains outside ordinary rarity rewards", "CardRarity.Ancient" in card)
    check(f"{label}: Biased Cognition portrait is reused", "ModelDb.Card<BiasedCognition>().PortraitPath" in card)

    check(f"{label}: upkeep runs once at player turn energy reset", "AfterEnergyReset(Player player)" in power and "player != Owner.Player" in power)
    check(f"{label}: upkeep applies negative Amount as Focus", "-Amount" in power and "ApplyPower<FocusPower>" in power)
    check(f"{label}: every negative Focus change is reduced by one", "TryModifyPowerAmountReceived" in power and "canonicalPower is not FocusPower" in power and "amount >= 0m" in power and "Math.Min(0m, amount + 1m)" in power)
    check(f"{label}: mitigation also affects this card's own upkeep", "PowerCmd.Apply<FocusPower>" not in power and "Bd.ApplyPower<FocusPower>" in power)

    check(f"{label}: custom card is registered but original 26-card set stays separate", "AddedCardTypes" in registry and "typeof(BdReworkedBiasedCognition)" in registry and "RestoredCardTypes" in registry)
    check(f"{label}: custom power is registered", "typeof(BdReworkedBiasedCognitionPower)" in registry)
    check(f"{label}: Darv override is Defect-only", "player.Character.CardPool is not DefectCardPool" in darv)
    check(f"{label}: Darv selects only the new card", "ModelDb.Card<BetterDefect.Cards.BdReworkedBiasedCognition>()" in darv and "__instance.AncientCard = eventCard.Id" in darv and "return false" in darv)
    check(f"{label}: Darv no longer selects vanilla Biased Cognition", "ModelDb.Card<BiasedCognition>" not in darv and "ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.BiasedCognition>" not in darv)

    check(f"{label}: Chinese card localization is present", '"偏差认知*改"' in localization and "获得 {FocusPower:diff()} 点[gold]集中[/gold]" in localization and "少失去 1 点" in localization)
    check(f"{label}: Chinese power localization is present", "BD_REWORKED_BIASED_COGNITION_POWER" in localization and "每当你将失去集中时，少失去 1 点" in localization)
    check(f"{label}: power icon fallback is present", "BdReworkedBiasedCognitionPower" in icons and "BIASED_COGNITION_POWER" in icons)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--v103-source", type=Path)
    parser.add_argument("--binary", type=Path, action="append", default=[])
    parser.add_argument("--mobile-binary", type=Path, action="append", default=[])
    parser.add_argument("--dusty-tome-source", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()

    passed: list[str] = []
    failed: list[str] = []

    def check(name: str, condition: bool) -> None:
        (passed if condition else failed).append(name)

    audit_source(SOURCE, "canonical", check)
    if args.v103_source:
        audit_source(args.v103_source, "Android v103 bridge", check)

    if args.dusty_tome_source:
        dusty = read(args.dusty_tome_source)
        check("game Dusty Tome upgrades the granted event card", "public override async Task AfterObtained()" in dusty and "CardCmd.Upgrade(card);" in dusty and "PileType.Deck" in dusty)

    for binary in args.binary:
        data = binary.read_bytes() if binary.is_file() else b""
        check(f"compiled binary exists: {binary}", len(data) > 100_000)
        check(f"compiled binary contains custom card metadata: {binary}", "BdReworkedBiasedCognition".encode("utf-8") in data)
    for binary in args.mobile_binary:
        data = binary.read_bytes() if binary.is_file() else b""
        check(f"compiled mobile binary exists: {binary}", len(data) > 100_000)
        check(f"compiled mobile binary contains custom card metadata: {binary}", "BdReworkedBiasedCognition".encode("utf-8") in data)
        if "v103" in str(binary).lower():
            check(f"v103 binary has no PC-only ICombatState metadata: {binary}", b"ICombatState" not in data)

    lines = [
        "BetterDefect v0.11.36 Darv card offline audit",
        f"Timestamp: {dt.datetime.now().astimezone().isoformat(timespec='seconds')}",
        f"Passed: {len(passed)}",
        f"Failed: {len(failed)}",
        "",
        "PASS",
        *[f"  [OK] {item}" for item in passed],
    ]
    if failed:
        lines.extend(["", "FAIL", *[f"  [FAIL] {item}" for item in failed]])
    report = "\n".join(lines) + "\n"
    print(report, end="")
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(report, encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
