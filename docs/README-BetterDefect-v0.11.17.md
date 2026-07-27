# BetterDefect v0.11.17

适用版本：

- 手机：杀戮尖塔 2 v0.103.2
- 电脑：杀戮尖塔 2 v0.107.1

## 本次修复

修复手机版在部分改造牌结算后无法继续打出卡牌的问题。

原因是旧的手机压缩包误装入了针对 PC v0.107.1 API 编译的 DLL。改造版“迭代”抽牌并消耗状态牌时，会引用手机 v0.103.2 中不存在的 `ICombatState` 类型，抛出 `TypeLoadException` 并堵塞战斗行动队列。

v0.11.17 已执行以下处理：

- 手机包严格使用 v0.103.2 的 `sts2.dll` 与 `0Harmony.dll` 独立编译。
- PC 包继续使用 v0.107.1 API 编译。
- 手机与 PC DLL 不再混用。
- 新增手机二进制审计：手机 DLL 出现 PC 专用 `ICombatState` 元数据时，发布检查直接失败。

## 验证

- PC 和手机分别编译成功，均为 0 个编译错误。
- 离线结构与行为路由审计：205/205 通过。
- 手机 DLL 中 `ICombatState` 元数据计数为 0。
- 已在 Android v0.103.2 真机启动，BetterDefect v0.11.17 的 32/32 个补丁类安装完成，启动日志未再出现 `TypeLoadException`、`ICombatState` 或 `MissingMethodException`。

## 安装

1. 选择与游戏平台对应的压缩包。
2. 解压后确认最外层包含 `BetterDefect` 文件夹。
3. 将该文件夹放入游戏的 `mods` 目录并启用。

不要把 PC 包安装到手机，也不要把手机包安装到 PC。
