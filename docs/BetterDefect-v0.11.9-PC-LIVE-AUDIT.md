# BetterDefect v0.11.9 电脑实机回归

- 游戏版本：Slay the Spire 2 v0.107.1
- 测试平台：Windows / Steam
- 测试方式：自动载入真实存档，使用游戏控制台进入战斗，在真实战斗中生成2个冰霜球、施加冰雹风暴能力并读取战斗历史

## 回归结果

| 测试项 | 期望 | 实际 |
| --- | --- | --- |
| 改造卡面文本 | 每个冰霜球分别造成伤害 | 通过 |
| 战斗状态栏文本 | 与卡面一致并显示 `{Amount}` | 通过 |
| 冰霜球数量 | 2 | 2 |
| 可攻击敌人数 | 1 | 1 |
| 独立伤害事件数 | 2 | 2 |
| 每次伤害 | 2 | 2、2 |

自动回归最终日志：

```text
CARD_TEXT=在你的回合结束时，每有1个冰霜充能球，就分别对所有敌人造成{HailstormPower:diff()}点伤害。
POWER_TEXT=在你的回合结束时，每有1个冰霜充能球，就分别对所有敌人造成{Amount}点伤害。
OBSERVE frost=2 enemies=1 events=2 expected=2 damages=[2,2]
PASS: card text, status text, and one-damage-event-per-Frost behavior agree.
```

测试结束后已恢复两份电脑存档及 BetterDefect 动态出率/改造配置，并从游戏 `mods` 目录移除临时自动测试模组。
