# BetterDefect v0.8.2 Android 真机验证

- 时间：2026-07-19T23:10:51+08:00
- 设备：REDMI K80 Pro（24122RKC7C）
- 游戏：`com.megacrit.stsx` v0.103.2 / versionCode 10302
- MOD：BetterDefect v0.8.2（无 BaseLib）

## 启动结果

- 日志确认 `loaded v0.8.2`。
- 日志确认 `Finished mod initialization`。
- 日志确认 `[Startup] Stage 14: game startup complete`。
- 本次成功运行的 `godot.log` 和当前进程日志未出现 `FATAL EXCEPTION`、`Fatal signal` 或 `SIGSEGV`。
- 主菜单显示已加载 8 个模组（共检测到 11 个，3 个由用户设置禁用）。

## 百科大全结果

- 机器人卡牌总数：114。
- 动态出率文本正常显示。
- 禁用卡牌灰化、禁用/启用按钮、卡面切换按钮、历史版本改造按钮正常出现。
- 35 点分段进度条正常显示：34/35；禁用 25 张、历史改造 9 张。
- 持久化状态成功读取：动态出率、禁用和历史改造设置没有因更新被重置。
- 搜索“裂变”时，普通牌只显示“移除所有充能球”，与实际基础行为一致。

## 保留的用户设置

- 启用：SpireBank、LoserEatDust、GreedyCredit、FrozenSnakeEye、CardBeautify、BetterDefect，以及用户自装 DefectSkin_AD、OldVerDoormaker。
- 用户设置中保持禁用：UnlockEverything、TurnRewind、TifiraDefectSkin。
- 未修改上述启用状态，未触碰 `files/BetterDefect`、`files/CardBeautify`、`files/mod_configs` 用户数据目录。
