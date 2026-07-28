# BetterDefect v0.11.20 Android 实机验证

- 设备：REDMI K80 Pro
- 游戏：Slay the Spire 2 v0.103.2
- 模组 DLL SHA256：`479FE956E6AC921A0E891B5E7206CCDB49514B56679A10C1E7007CC86455AEF7`

结果：

- 游戏进程正常启动。
- BetterDefect v0.11.20 的 32/32 个延迟补丁全部安装。
- 26 张一代卡牌全部注入。
- 机器人卡池为 114 张，`poolRestored=26/26`，`globalDefect=114`。
- 合并 45 条卡牌本地化与 15 条能力本地化。
- 百科大全机器人筛选实际显示 114 张牌。
- 打开百科大全后没有 `LocException`、`Failed to compare` 或 BetterDefect 错误。
