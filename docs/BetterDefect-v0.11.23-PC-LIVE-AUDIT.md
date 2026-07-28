# BetterDefect v0.11.23 PC 实战验证

- 时间：2026-07-28 13:07 (Asia/Shanghai)
- 平台：Steam PC v107.1
- 场景：启用改造版“野性”，施加 1 层野性，将能力牌“碎片整理”设为 0 费并实际打出，等待能力飞行动画完全结束后检查后端牌堆与 Godot 手牌节点。

## 观测结果

```text
OBSERVE pile=Hand beforeNode=102139695567 afterNode=102139695567 matchingNodes=1 valid=True queued=False visible=True treeVisible=True scale=(1, 1) modulateA=1.00
PASS: transformed Feral returned a zero-cost Power with one visible, full-scale, non-deleting hand node.
```

结论：能力牌正确回到手牌；回手前后保持同一个真实卡牌节点，场上仅存在 1 个对应节点；节点未进入删除队列、可见、缩放为 1、透明度为 1。

## 收尾

- 临时测试模组已从游戏 `mods` 目录移除。
- 测试前的当前局、备份存档和 BetterDefect 动态出率/改造配置已恢复并校验哈希。
