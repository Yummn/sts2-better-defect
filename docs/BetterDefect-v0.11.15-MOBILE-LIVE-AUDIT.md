# BetterDefect v0.11.15 手机实机验证

验证环境：

- 设备：REDMI K80 Pro
- 游戏包名：`com.megacrit.stsx`
- 游戏版本：v0.103.2
- BetterDefect：v0.11.15

验证结果：

- 已清除手机 `mods` 目录中的旧版重复副本和临时测试模组。
- 已恢复测试前的玩家存档与 BetterDefect 配置。
- 正式模组冷启动完成，日志到达 `Stage 14: game startup complete`。
- Android 延迟补丁队列完成：`32/32 classes installed`。
- 持久化改造状态恢复成功：`upgrades=49, points=50/50`。
- 动态出率配置读取成功：`cards=114, disabled=1`。
- 改造版“吞噬暗影+”已进行手机实战命令验证：
  - 记录的重复次数为 `repeat=3`；
  - 实际生成黑暗球数量为 `darkOrbs=3`；
  - 测试结论为 `PASS`。
- “循环”单球双触发、改造状态重载、附魔优先级、玻璃球生成等回归检查由离线审计覆盖。
- 源码/行为离线审计：196 项通过；加入电脑和手机二进制检查后为 198 项通过，0 项失败。

安装后的正式版 DLL SHA-256：

`AAAFBC3EE1BB9DAFAE8C046DDDB49A72A53986B3C9A88868BD89FE87A4DAB847`
