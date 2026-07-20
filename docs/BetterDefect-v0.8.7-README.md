# BetterDefect v0.8.7

- 修复 Android v103 打出“打碎 / Shatter”时出现 `ICombatState` TypeLoadException、卡牌卡住及随后原生崩溃的问题。
- 多目标攻击与敌人列表改为运行时适配 v103 `CombatState` / PC v107 `ICombatState`，DLL 不再包含 PC 专用 `ICombatState` 类型引用。
- 同步修复电动力学全体闪电路径中的同类跨版本引用。
- 保留 26 张旧版机器人卡、动态出率、卡牌禁用、35 点历史版本升级和卡面状态持久化。
