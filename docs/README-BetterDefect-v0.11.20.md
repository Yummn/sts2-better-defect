# BetterDefect v0.11.20

## 本次修复

- 修复 Android v103 中一代机器人卡牌已注入卡池、但百科大全仍显示 0 张或排序失败的问题。
- Android 延迟补丁队列完成时，显式向 `LocManager` 合并 45 条卡牌文本与 15 条能力文本。
- 修复 `LocManager.Initialize` 早于 Harmony 本地化补丁安装，导致 `BD_CONSUME.title` 等键缺失的问题。
- 百科大全若已提前建立旧卡牌快照，会自动替换为当前 `ModelDb.AllCards` 并重新执行筛选。
- 保留卡图选择、禁用选择、动态出率和改造状态，不覆盖玩家持久化配置。

## 兼容版本

- 手机：杀戮尖塔2 v0.103.2
- 电脑：杀戮尖塔2 v0.107.1

## 验证结果

- 离线行为审计：205/205。
- PC 与 Android 分别使用各自游戏 API 编译，均为 0 errors。
- REDMI K80 Pro 启动日志：32/32 补丁完成，`pool=114`、`poolRestored=26/26`、`globalDefect=114`。
- 真机百科大全机器人筛选显示 114 张牌。
- 日志确认本地化合并完成，进入百科大全后没有 `LocException` 或 `Failed to compare`。
