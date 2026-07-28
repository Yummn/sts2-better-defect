# BetterDefect v0.11.19

## 本次修复

- 修复 Android v103 延迟安装 Harmony 补丁后，百科大全机器人筛选可能显示 `0张牌` 或只显示原版 88 张牌的问题。
- 重建机器人卡池后，为全部 114 张卡显式恢复 `DefectCardPool` 归属。
- 保留 `ModelDb.Preload` 已完成的全局卡牌快照，只合并替换机器人卡池，避免其他角色卡池与百科筛选失效。
- 启动日志校验 `pool=114`、`poolRestored=26/26`、`globalRestored=26/26`、`globalDefect=114`。

## 兼容版本

- 手机：杀戮尖塔2 v0.103.2
- 电脑：杀戮尖塔2 v0.107.1

