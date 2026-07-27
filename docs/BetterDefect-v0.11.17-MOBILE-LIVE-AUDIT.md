# BetterDefect v0.11.17 Android v103 真机审计

- 设备包名：`com.megacrit.stsx`
- 游戏版本：`0.103.2`
- 模组版本：`0.11.17`
- Android 补丁队列：32/32 安装完成
- 手机 DLL 编译目标：本机提取的 v0.103.2 API
- 手机 DLL 内 `ICombatState` 元数据：0
- 启动后检查：
  - `TypeLoadException`：未出现
  - `ICombatState` 加载错误：未出现
  - `MissingMethodException`：未出现

## 根因

v0.11.16 的手机资产与 PC 资产使用了相同 DLL。改造版“迭代”的完成回调在手机上尝试解析 PC API 中的 `MegaCrit.Sts2.Core.Combat.ICombatState`，导致 `TypeLoadException`。异常发生在行动队列任务内，之后表现为卡牌无法打出。

## 修复

手机和 PC 改为真正独立构建，并在发布前对手机 DLL 执行 PC 专用类型元数据拒绝检查。
