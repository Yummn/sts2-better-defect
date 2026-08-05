#!/usr/bin/env python3
"""Audit BetterDefect card-face text against transformed gameplay routes."""

from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT if (ROOT / "BetterDefectCode").is_dir() else ROOT / "src"


def main() -> int:
    versions = (SOURCE / "BetterDefectCode" / "CardVersionUpgrades.cs").read_text(encoding="utf-8-sig")
    localization = (SOURCE / "BetterDefectCode" / "Localization.cs").read_text(encoding="utf-8-sig")
    manifest = json.loads((SOURCE / "BetterDefect.json").read_text(encoding="utf-8-sig"))

    expected_descriptions = {
        "ROCKET_PUNCH", "SHATTER", "TESLA_COIL", "FUEL", "SCRAPE", "BARRAGE",
        "BEAM_CELL", "CHARGE_BATTERY", "COLD_SNAP", "COOLHEADED", "GO_FOR_THE_EYES",
        "GUNK_UP", "LEAP", "LIGHTNING_ROD", "SWEEPING_BEAM", "UPROAR", "BD_RECURSION",
        "BD_RECYCLE", "BD_STREAMLINE", "CHAOS", "DOUBLE_ENERGY", "FIGHT_THROUGH", "SKIM",
        "TEMPEST", "WHITE_NOISE", "FTL", "NULL", "REFRACT", "FERAL", "HAILSTORM",
        "ITERATION", "LOOP", "SMOKESTACK", "STORM", "SUBROUTINE", "BD_REPROGRAM",
        "BD_STATIC_DISCHARGE", "BULK_UP", "HELIX_DRILL", "BD_REINFORCED_BODY", "SYNTHESIS",
        "SUNDER", "BD_MELTER", "BD_BULLSEYE", "RIP_AND_TEAR", "HYPERBEAM", "SPINNER",
        "ADAPTIVE_STRIKE", "ALL_FOR_ONE", "BUFFER", "CONSUMING_SHADOW", "COOLANT",
        "CREATIVE_AI", "ECHO_FORM", "FLAK_CANNON", "METEOR_STRIKE", "MULTI_CAST", "RAINBOW",
    }
    actual_descriptions = set(re.findall(r'\["([A-Z0-9_]+)\.description"\]\s*=', localization))

    fuel_play = versions[versions.index("internal static async Task Play(Fuel card"):]
    checks = {
        "manifest is v0.11.40": manifest.get("version") == "0.11.40",
        "every behavior-changing card has a card-face override": expected_descriptions <= actual_descriptions,
        "Uproar summary limits selection to attacks": "1(2)张当前耗能最高的攻击牌" in versions,
        "Uproar card face uses upgrade-aware count and attack restriction": (
            "{IfUpgraded:show:2|1}张当前耗能最高的攻击牌" in localization
        ),
        "Uproar implementation filters attack cards": "c.Type == CardType.Attack" in versions,
        "Loop card and power text allow one orb to trigger from both edges": "同一个充能球" not in localization,
        "Synthesis card face describes upgrade selection without editorial parentheses": (
            "{IfUpgraded:show:从[gold]抽牌堆[/gold]中选择1张能力牌加入手牌。|从[gold]抽牌堆[/gold]中随机抽1张能力牌。}"
            in localization
            and "随机抽1张{IfUpgraded" not in localization
        ),
        "Fuel card face uses only native dynamic variables": (
            "抽{IfUpgraded:show:2|1}张牌" in localization
            and "ResolveFuelDrawCount(card)" in fuel_play
            and 'DynamicVars["Cards"]' not in fuel_play
        ),
        "card-face text contains no user-draft parenthesized upgrade counts": not re.search(
            r'\["[A-Z0-9_]+\.description"\].{0,250}[0-9X]+（[0-9X+]+）', localization, re.S
        ),
    }

    failed = [name for name, passed in checks.items() if not passed]
    for name, passed in checks.items():
        print(f"  [{'OK' if passed else 'FAIL'}] {name}")
    missing = sorted(expected_descriptions - actual_descriptions)
    if missing:
        print(f"  Missing card-face overrides: {', '.join(missing)}")
    print(f"Passed: {len(checks) - len(failed)}; Failed: {len(failed)}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
