# 更好的故障机器人（Better Defect）

这是我围绕《杀戮尖塔 2》故障机器人做的一组内容补完和玩法改造。项目最初只是想找回旧版本里消失的卡牌，后来逐步加入了可选卡牌改造、开局牌组调整和一些跨平台修复。所有改造都会明确显示在百科中，不需要 BaseLib。

## 主要功能

- 恢复 26 张旧版故障机器人卡牌，并把它们重新加入对应卡池。
- 为达尔文的尘封典籍加入故障机器人专属先古卡“偏差认知*改”。
- 提供 59 项可选卡牌改造，按普通、超频和过载三个阶段共用 50 点预算；选择会跨局保存。
- 可把开局的一张“打击”替换为“球状闪电”。
- 为 CardBeautify 提供部分旧版卡图兼容，并修复“裂变”充能球的视觉同步问题。

动态奖励概率和卡牌禁用已经从本项目拆出，请使用独立的 [Dynamic Card Odds](https://github.com/Yummn/sts2-dynamic-card-odds)。从 v0.11.35 起，这两项功能不会再占用 BetterDefect 的改造点数。

## 当前版本

推荐使用 [v0.11.50](https://github.com/Yummn/sts2-better-defect/releases/tag/v0.11.50)。这一版调整了改造后“回收”的升级方式：费用保持 1，升级改为移除“消耗”；未改造的旧版“回收”仍维持原来的 1（0）费和“消耗”。

项目分别维护以下构建：

- Android v0.103.2
- Android v0.110.1
- PC v0.107.1

三个版本引用的游戏接口不同，必须下载文件名与平台相符的安装包，不能交换 DLL。每个 Release ZIP 都包含可直接安装的 `BetterDefect` 文件夹。

## 安装与使用

1. 从 [GitHub Releases](https://github.com/Yummn/sts2-better-defect/releases) 下载对应平台的 ZIP。
2. 手机启动器可以直接导入完整 ZIP；手动安装时，把其中的 `BetterDefect` 文件夹复制到游戏的 `mods` 目录。
3. 进入百科大全查看恢复卡牌和改造选项。改造选择会保存到游戏用户数据目录，更新 MOD 时无需重新选择。

这个项目改动的卡牌和兼容分支较多，更新前建议保留一份存档与 MOD 数据备份。

## 从源码构建

源码位于 `src/`。准备好目标版本的游戏程序集后运行：

```powershell
dotnet build src/BetterDefect.csproj -c Release
```

可通过 MSBuild 属性指定本机游戏路径和输出目录。自动检查脚本、实机记录与历史排错资料分别保存在 `tests/`、`docs/` 和 `reports/`；详细版本变更以 [Releases](https://github.com/Yummn/sts2-better-defect/releases) 为准，不再在首页重复堆放完整日志。
