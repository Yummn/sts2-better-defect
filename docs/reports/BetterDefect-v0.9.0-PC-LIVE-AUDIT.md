# BetterDefect v0.9.0 PC live audit

- Target: Slay the Spire 2 v0.107.1 (release commit `59260271`)
- Result: main menu reached successfully; no runtime `ERROR`, exception, Harmony exception, missing-method error, or target-invocation error.
- BetterDefect initialization reported v0.9.0 and applied both new patches:
  - `BdCustomCommonCardPlayPatch`
  - `BdCustomBeamCellHoverTipsPatch`
- Model registration reported 26/26 restored Defect cards and expanded the Defect card library from 88 to 114 cards.
- Encyclopedia validation:
  - custom controls appeared on the Defect card page;
  - Barrage and Beam Cell switched from red `改造：关闭` to green `改造：自定义`;
  - the shared point counter changed from 0/35 to 1/35;
  - switching each option off refunded the point and restored 0/35.
- Test switches were restored to off before exit.

The twelve combat routes are additionally covered by the 96/96 source and dual-binary offline audit.
