#!/usr/bin/env python3
"""Focused structural and binary audit for the transformed Uproar regression."""

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


def method_body(source: str, signature: str) -> str:
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[brace + 1:index]
    raise AssertionError(f"unclosed method: {signature}")


def audit_source(source_root: Path, label: str, check) -> None:
    versions = read(source_root / "BetterDefectCode" / "CardVersionUpgrades.cs")
    localization = read(source_root / "BetterDefectCode" / "Localization.cs")
    manifest = json.loads(read(source_root / "BetterDefect.json"))
    play = method_body(versions, "private static async Task PlayUproar")
    cost = method_body(versions, "private static int GetCurrentUproarEnergyCost")

    check(f"{label}: manifest v0.11.38", manifest.get("version") == "0.11.38")
    check(f"{label}: damage remains five before and after upgrade", 'SetDynamic(card, "Damage", upgradedVersion ? 5m : plus ? 7m : 5m)' in versions and 'UpgradeDynamicTo(card, "Damage", upgradedVersion ? 5m : 7m)' in versions)
    check(f"{label}: Android ID fallback also keeps transformed damage at five", 'case "CARD.UPROAR":' in versions and versions.count('upgradedVersion ? 5m : plus ? 7m : 5m') >= 2 and versions.count('upgradedVersion ? 5m : 7m') >= 2)
    check(f"{label}: damage is dealt exactly twice", "WithHitCount(2)" in play and "card.DynamicVars.Damage.BaseValue" in play)
    check(f"{label}: normal and upgraded autoplay counts are one and two", "card.IsUpgraded ? 2 : 1" in play)
    check(f"{label}: draw pile is re-read for each autoplay", "for (var i = 0; i < playCount; i++)" in play and play.index("PileType.Draw.GetPile") > play.index("for (var i = 0; i < playCount; i++)"))
    check(f"{label}: all playable card types are eligible", "c.Type == CardType.Attack" not in play and "CardKeyword.Unplayable" in play)
    check(f"{label}: highest current cost is selected", "playable.Max(GetCurrentUproarEnergyCost)" in play and "GetCurrentUproarEnergyCost(c) == highestCost" in play)
    check(f"{label}: ties are randomized with run RNG", "StableShuffle(card.Owner.RunState.Rng.Shuffle)" in play)
    check(f"{label}: two selections cannot reuse the same card instance", "alreadySelected" in play and "alreadySelected.Add(selected)" in play)
    check(f"{label}: selected cards are actually auto-played", "await CardCmd.AutoPlay(choiceContext, selected, null)" in play)
    check(f"{label}: X cost ranks as current available energy", "card.EnergyCost.CostsX" in cost and "card.Owner.PlayerCombatState.Energy" in cost)
    check(f"{label}: fixed costs include all combat modifiers", "GetWithModifiers(CostModifiers.All)" in cost)
    check(f"{label}: card description matches 5 damage and 1(2) highest-cost cards", "造成{Damage:diff()}点伤害两次" in localization and "当前费用最高的1（2）张牌" in localization)
    check(f"{label}: transformation summary matches gameplay", "造成5点伤害两次；随机打出抽牌堆中1(2)张当前费用最高的牌" in versions)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--v103-source", type=Path)
    parser.add_argument("--binary", type=Path, action="append", default=[])
    parser.add_argument("--mobile-binary", type=Path, action="append", default=[])
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()

    passed: list[str] = []
    failed: list[str] = []

    def check(name: str, condition: bool) -> None:
        (passed if condition else failed).append(name)

    audit_source(SOURCE, "canonical", check)
    if args.v103_source:
        audit_source(args.v103_source, "Android v103 bridge", check)

    for binary in args.binary:
        data = binary.read_bytes() if binary.is_file() else b""
        check(f"compiled binary exists: {binary}", len(data) > 100_000)
        check(f"compiled binary contains Uproar route metadata: {binary}", b"GetCurrentUproarEnergyCost" in data)
    for binary in args.mobile_binary:
        data = binary.read_bytes() if binary.is_file() else b""
        check(f"compiled mobile binary exists: {binary}", len(data) > 100_000)
        check(f"compiled mobile binary contains Uproar route metadata: {binary}", b"GetCurrentUproarEnergyCost" in data)
        if "v103" in str(binary).lower():
            check(f"v103 binary has no PC-only ICombatState metadata: {binary}", b"ICombatState" not in data)

    lines = [
        "BetterDefect v0.11.38 transformed Uproar audit",
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
