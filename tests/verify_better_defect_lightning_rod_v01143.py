#!/usr/bin/env python3
"""Focused source and binary audit for transformed Lightning Rod v0.11.43."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def audit_source(source_root: Path, label: str, check) -> None:
    versions = (source_root / "BetterDefectCode" / "CardVersionUpgrades.cs").read_text(
        encoding="utf-8-sig"
    )
    manifest = json.loads((source_root / "BetterDefect.json").read_text(encoding="utf-8-sig"))

    check(f"{label}: manifest is v0.11.43", manifest.get("version") == "0.11.43")
    check(
        f"{label}: encyclopedia summary is 5(8) Block",
        versions.count("获得5(8)格挡；立即生成1闪电，下回合再生成1闪电") >= 2,
    )
    check(
        f"{label}: normal and Android fallback setup use 5(8) Block",
        len(re.findall(r"upgradedVersion\s*\?\s*plus \? 8m : 5m", versions)) >= 2,
    )
    check(
        f"{label}: normal and Android fallback upgrade routes set 8 Block",
        versions.count('UpgradeDynamicTo(card, "Block", upgradedVersion ? 8m : 7m)') >= 2,
    )
    check(
        f"{label}: obsolete transformed 5(6) values are absent",
        "获得5(6)格挡" not in versions
        and "plus ? 6m : 5m" not in versions
        and 'UpgradeDynamicTo(card, "Block", upgradedVersion ? 6m : 7m)' not in versions,
    )


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

    audit_source(ROOT / "src", "canonical", check)
    if args.v103_source:
        audit_source(args.v103_source, "Android v103 bridge", check)

    for binary in args.binary:
        check(f"compiled binary exists: {binary}", binary.is_file() and binary.stat().st_size > 100_000)
    for binary in args.mobile_binary:
        data = binary.read_bytes() if binary.is_file() else b""
        check(f"compiled mobile binary exists: {binary}", len(data) > 100_000)
        if "v103" in str(binary).lower():
            check(f"v103 binary has no PC-only ICombatState metadata: {binary}", b"ICombatState" not in data)

    lines = [
        "BetterDefect v0.11.43 transformed Lightning Rod audit",
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
