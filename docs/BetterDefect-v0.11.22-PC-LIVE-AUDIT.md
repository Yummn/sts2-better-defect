# BetterDefect v0.11.22 PC 实战验证

- 时间：2026-07-28 12:44 (Asia/Shanghai)
- 平台：Steam PC v107.1
- 模组：BetterDefect v0.11.22
- 场景：强制进入 4 敌人战斗，实际打出 `BD_ELECTRODYNAMICS`，分别触发闪电球被动与激发。

## 观测结果

```text
OBSERVE enemies=4 passive=[3,3,3,3] evoke=[8,8,8,8]
PASS: Electrodynamics Lightning passive and evoke both damaged every hittable enemy.
```

结论：4 个可命中敌人均受到闪电球被动 3 点伤害，随后均受到激发 8 点伤害；电动力学的两条结算路径均正常全体命中。

## 收尾

- 自动测试模组已从游戏 `mods` 目录移除。
- 测试前的当前局、备份存档和 BetterDefect 动态出率数据已恢复。
