# BetterDefect v0.8.2

适配：手机版 v0.103.2、电脑版 v0.107.1；不依赖 BaseLib。

## 本版内容

- 完整保留 v0.8.1 的卡牌描述/实际效果一致性修复：火箭飞拳、特斯拉线圈、燃料/压缩、刮削、裂变、核心电涌和增幅。
- 手机版构建跳过 `NCard.Model` setter 的 Harmony detour。该 detour 在 v103 Android ARM64 的 MonoMod/Harmony 动态补丁阶段可能触发原生崩溃；百科大全仍由专用 `NCardLibraryGrid` 刷新路径维护动态出率、禁用、卡面和历史改造 UI。
- 电脑版继续保留该 setter hook，不改变 PC 行为。
- 用户数据仍保存在独立目录，覆盖 MOD 不会重置卡图选择、禁用状态、动态出率或历史版本改造。

## 审计与真机验证

- 逐项审计 26 张恢复卡牌及 14 项历史版本改造。
- 自动化离线审计：67 项通过，0 项失败。
- PC v107.1 与手机版 v103 分别编译：均为 0 错误（仅 12 条 nullable warning）。
- 反编译确认：PC `Prepare()` 返回 `true`，手机版 `Prepare()` 返回 `false`，Android 不会生成该 setter detour。
- REDMI K80 Pro / 游戏 v0.103.2 真机启动成功：BetterDefect v0.8.2 完成初始化，启动阶段到达 Stage 14。
- 真机百科大全显示 114 张机器人卡牌；动态出率、禁用灰化/按钮、卡面按钮、历史版本改造按钮与 35 点分段进度条均出现；持久状态为禁用 25、历史改造 9、共 34/35 点。
- 真机搜索“裂变”确认普通版只显示“移除所有充能球”，不再同时显示激发行为或重复消耗行。

## 安装

- 手机版 v103：`BetterDefect-v0.8.2.zip`
- 电脑版 v107.1：`BetterDefect-v0.8.2-PC-v107.1.zip`
- 解压或导入后，把 `BetterDefect` 文件夹放入游戏 `mods/`。

ZIP 只包含使用 `/` 的文件条目且不写目录条目，兼容手机版设置页导入器。
