# BetterDefect v0.8.7

## 修复

- 修复 Android v103 打出“打碎 / Shatter”时因引用 PC 专用 `ICombatState` 而无法结算、卡牌卡住并可能导致原生崩溃的问题。
- 全体敌人目标选择改为运行时适配：Android v103 使用具体 `CombatState`，当前 PC 版使用 `ICombatState`。
- 电动力学的全体闪电路径同步改为跨版本敌人列表获取。

## 验证

- 离线回归：76/76。
- DLL 元数据：`ICombatState` TypeRef = 0。
- REDMI K80 Pro / Android v0.103.2 实机：打碎造成 11 点全体伤害，随后同一闪电充能球触发两次 8 点伤害，卡牌正常进入弃牌堆，进程存活，崩溃缓冲为空。

## 安装

下载 `BetterDefect-v0.8.7-Mobile-v103.zip`，将压缩包内的 `BetterDefect` 文件夹放入游戏 `mods/` 目录。
