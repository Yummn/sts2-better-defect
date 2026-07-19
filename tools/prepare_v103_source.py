#!/usr/bin/env python3
"""Create the source-compatible v0.103.2 build tree without touching Steam."""

from __future__ import annotations

import argparse
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", default="0.8.0")
    args = parser.parse_args()

    target = (ROOT / ".mobilebuild" / f"src-v103-{args.version}").resolve()
    mobile_root = (ROOT / ".mobilebuild").resolve()
    if mobile_root not in target.parents:
        raise RuntimeError(f"refusing to replace path outside .mobilebuild: {target}")
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True)

    for name in (
        "BetterDefect.csproj",
        "BetterDefect.json",
        "BetterDefect.DynamicOdds.cfg",
        "Sts2PathDiscovery.props",
        "project.godot",
    ):
        shutil.copy2(ROOT / name, target / name)
    shutil.copytree(ROOT / "BetterDefectCode", target / "BetterDefectCode")
    shutil.copytree(ROOT / "BetterDefectData", target / "BetterDefectData")

    cards_path = target / "BetterDefectCode" / "CardsAndPowers.cs"
    cards = cards_path.read_text(encoding="utf-8")
    # v0.103.2 exposes CardModel's overridable members as public; newer PC
    # builds changed those members to protected.
    cards = cards.replace("protected override", "public override")
    # PowerModel used AfterTurnEnd(ctx, side) in v0.103.2 and renamed/expanded
    # it to AfterSideTurnEnd(ctx, side, participants) in current PC builds.
    cards = cards.replace(
        "public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)",
        "public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)",
    )
    cards = cards.replace(
        "public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)",
        "public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)",
    )
    cards_path.write_text(cards, encoding="utf-8")

    print(target)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
