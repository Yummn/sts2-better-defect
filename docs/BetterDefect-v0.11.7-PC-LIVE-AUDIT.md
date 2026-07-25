# BetterDefect v0.11.7 电脑实机回归

- 游戏版本：Slay the Spire 2 v0.107.1
- 测试平台：Windows / Steam
- 测试方式：在真实战斗中自动施加能力、生成状态牌、生成充能球并读取战斗历史

## 修复前复现

- 两张改造烟囱叠加：能力伤害数值为 8。
- 首次生成状态牌：实际只额外抽 1 张。
- 结论：伤害层数正确，首次触发的抽牌被写死为 1。

## v0.11.7 回归结果

| 测试项 | 期望 | 实际 |
| --- | ---: | ---: |
| 两张烟囱叠加后的伤害 | 8 | 8 |
| 烟囱本回合首次触发的额外抽牌 | 2 | 2 |
| 两层子程序首次触发的额外抽牌 | 2 | 2 |
| 两层子程序触发的额外能量 | 2 | 2 |
| 两层循环、两个边缘充能球的被动触发次数 | 4 | 4 |

自动回归最终日志：

```text
OBSERVE SMOKESTACK amount=8 firstTriggerDraws=2
OBSERVE SUBROUTINE amount=2 firstTriggerDraws=2 energyDelta=2
OBSERVE LOOP amount=2 orbs=2 edgePassiveTriggers=4
SUMMARY smokeDraw=2 subDraw=2 subEnergy=2 loopTriggers=4
PASS: all transformed power stack regressions passed.
```

测试结束后已恢复两份电脑存档及 BetterDefect 动态出率配置，并从游戏 `mods` 目录移除临时自动测试模组。

