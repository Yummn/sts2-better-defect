# BetterDefect v0.8.6 Android 实机验证

- 游戏：Slay the Spire 2 v0.103.2 Android
- 设备：REDMI K80 Pro（arm64 / Vulkan）
- 连续冷启动：5/5 成功，全部到达 `Stage 14: game startup complete`
- 启动崩溃：0 次 SIGABRT / SIGSEGV
- 百科大全：故障机器人 114 张卡正常显示
- 恢复卡牌：26/26 注入成功
- 历史版本升级：8 项已存储，33/35 点正常恢复
- 动态出率、禁用遮罩、禁用按钮、升级按钮和卡面切换按钮均正常显示
- 中文本地化：未出现 `LocException`

本版将两个 `ModelDb.Init` Harmony 钩子合并为一个，减少 Android ARM64 启动期原生跳板数量，同时保留完整功能。

DLL SHA256：`fdf9473a2893e4bbbbe7c788e6661e60984b01d3aca47d4c550d8764a4a34c25`
