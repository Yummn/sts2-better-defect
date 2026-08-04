#!/usr/bin/env python3
"""Focused regression audit for transformed Compact's generated Fuel."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT if (ROOT / "BetterDefectCode").is_dir() else ROOT / "src"


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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--binary", type=Path, action="append", default=[])
    args = parser.parse_args()

    versions = (SOURCE / "BetterDefectCode" / "CardVersionUpgrades.cs").read_text(encoding="utf-8-sig")
    localization = (SOURCE / "BetterDefectCode" / "Localization.cs").read_text(encoding="utf-8-sig")
    manifest = json.loads((SOURCE / "BetterDefect.json").read_text(encoding="utf-8-sig"))
    play = method_body(versions, "internal static async Task Play(Fuel card")
    description = localization[
        localization.index('["FUEL.description"]'):localization.index('["SCRAPE.description"]')
    ]

    checks = {
        "manifest is v0.11.38": manifest.get("version") == "0.11.38",
        "draw count follows transformed Compact and Fuel upgrade state": (
            "ResolveFuelDrawCount(Fuel card)" in versions
            and "IsCompactVersionEnabled() ? card.IsUpgraded ? 2 : 1 : 0" in versions
        ),
        "Fuel play uses the resolver": "ResolveFuelDrawCount(card)" in play,
        "Fuel play never indexes the absent Cards variable": 'DynamicVars["Cards"]' not in play,
        "Fuel text uses the native upgrade selector": "抽{IfUpgraded:show:2|1}张牌" in description,
        "Fuel text never formats the absent Cards variable": "{Cards:" not in description,
    }

    for binary in args.binary:
        data = binary.read_bytes() if binary.is_file() else b""
        checks[f"binary exists: {binary}"] = len(data) > 100_000
        checks[f"binary contains Fuel resolver: {binary}"] = b"ResolveFuelDrawCount" in data

    failed = [name for name, passed in checks.items() if not passed]
    for name, passed in checks.items():
        print(f"  [{'OK' if passed else 'FAIL'}] {name}")
    print(f"Passed: {len(checks) - len(failed)}; Failed: {len(failed)}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
