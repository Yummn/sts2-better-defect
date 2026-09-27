# BetterDefect v0.11.65

## 修复内容
- 修复 Android 游戏 v0.111.0 抽牌后战斗回合中断、无法打出卡牌的问题。
- v111 的 CardCmd.Exhaust 返回值从 Task 改为 Task<CardPileAddResult?>；旧 DLL 的直接调用在 JIT 阶段报 MissingMethodException，甚至没有迭代能力也会阻断回合开始抽牌。
- 所有六处主动消耗统一经过一次缓存的方法解析，等待原生任务，不跳过消耗动画、能力或遗物回调，不吞掉异常。
- 保留 v0.11.64 的弹幕齐射修复：2（3）点临时集中持续至本回合结束，随后恢复，而不是触发充能球后立即移除。

## 适用版本
分别提供手机 v110.1、手机 v111.0、电脑 v107.1 包。请选择对应版本，把 BetterDefect 文件夹完整放入 mods 目录。
无需 BaseLib。不会覆盖卡牌改造选择、卡图选择或动态出率存档。

## 验证范围
REDMI K80 Pro，启动器 1.9 / versionCode 111，实际游戏载荷 v0.111.0。
旧 BetterDefect v0.11.63 + 原先启用的其他六个模组复现回合循环中断；只替换 BetterDefect 后，闪电、防御、回收消耗、迭代、快速检索与连续换回合正常，状态牌消耗正常。
实际运行时同时保留 EventSkip、SpireBank、LoserEatDust、FrozenSnakeEye、CardBeautify、DynamicCardOdds；未启用的其他模组不在本次战斗验证范围。
三目标构建均无错误（已有42条可空警告）。最终 DLL 检查确认无直接 CardCmd.Exhaust MemberRef；原生返回类型在 v110/PC107 为 Task，v111 为 Task<T>。
旧总量源码检查存在过期的预算/UI断言，不作为此次兼容修复的通过依据；以专项 ABI 检查和上述实机操作为准。
