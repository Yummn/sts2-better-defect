# BetterDefect

> 项目分类：个人项目 / 《杀戮尖塔 2》Mod / 故障机器人扩展 / 持续维护

BetterDefect 是一个面向《杀戮尖塔 2》故障机器人的大型扩展 Mod，主要用于恢复旧卡、增加卡牌改造系统，并补充部分角色与视觉相关功能。

## 主要功能

- 恢复 26 张旧版故障机器人卡牌；
- 为 Darv 增加故障机器人专属 Ancient 卡 `偏差认知*改`；
- 为 CardBeautify 提供旧版故障机器人卡图；
- 提供持久化的 60 点三阶段卡牌改造系统；
- 支持起始打击牌替换为球状闪电；
- 修复 Fission 等部分充能球相关视觉表现；
- 持续适配不同 PC / Android 游戏版本。

动态奖励出率与卡牌禁用功能已经从 BetterDefect 拆分到独立项目：

- [DynamicCardOdds](https://github.com/Yummn/sts2-dynamic-card-odds)

## 下载与安装

1. 打开 [Releases](https://github.com/Yummn/sts2-better-defect/releases)。
2. 下载与自己游戏平台和版本一致的 ZIP。
3. 解压后，将其中的 `BetterDefect` 文件夹完整复制到游戏 `mods/` 目录。

不同游戏版本的 DLL 不可混用。

## 当前兼容版本

当前发布包覆盖：

- Android v0.103.x
- Android v0.110.1
- Android v0.111.0（已实机验证）
- PC v0.107.1

以具体 Release 文件名和说明为准。

## 当前版本

当前 README 整理时的最新版本为 `v0.11.65`。

该版本修复了 v111 消耗接口变动导致的回合中断/无法出牌，并保留弹幕齐射临时集中持续至回合结束的修复。详见 [更新与测试说明](docs/release-v0.11.65.md)。

完整版本变更、兼容性测试和每次实机验证记录请直接查看：

- [GitHub Releases](https://github.com/Yummn/sts2-better-defect/releases)

## 说明

BetterDefect 迭代频率较高，具体卡牌数值、改造规则和兼容状态可能随游戏版本变化。README 只保留当前用户最需要的信息，详细变更统一记录在对应 Release 中。
