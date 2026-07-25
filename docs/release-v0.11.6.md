# BetterDefect v0.11.6

## 兼容版本

- Android：Slay the Spire 2 v0.103.2（继续使用 v0.11.5 Release 提供的 AOT Card-Play Bridge APK）
- PC：Slay the Spire 2 v0.107.1
- 不需要 BaseLib

## 修复

- 修复改造版“迭代”首次抽到状态牌后卡死、后续卡牌无法打出的问题。
- 旧实现从 `IterationPower.AfterCardDrawn` 内部立即消耗状态牌，导致该牌在 `CardPileCmd.Draw` 尚未执行 `CardModel.InvokeDrawn()` 时就离开手牌，抽牌动画与动作队列无法正确收尾。
- 新实现仅包装完整的 `CardPileCmd.Draw` 任务：等待外层抽牌及迭代触发的额外抽牌全部结束，再消耗本回合首次抽到且仍在手牌中的状态牌。

## 验证

- PC v0.107.1 实战自动回归：打出迭代成功。
- 抽到眩晕后，额外抽牌完成，眩晕进入消耗堆。
- 随后自动打出电击成功，动作队列未锁死。
- PC 与 Android v0.103.2 分别编译成功。
- 离线源代码、注册表、行为路由和二进制审计：174/174。
