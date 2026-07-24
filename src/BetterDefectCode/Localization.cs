using System.Collections.Generic;

using System;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace BetterDefect;

internal static class BdLocalization
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["cards/BD_RECURSION.title"] = "递归",
        ["cards/BD_RECURSION.description"] = "[gold]激发[/gold]下一个充能球，然后重新充能同类型充能球。",
        ["cards/BD_STEAM_BARRIER.title"] = "蒸汽护壁",
        ["cards/BD_STEAM_BARRIER.description"] = "获得 {Block:diff()} 点[gold]格挡[/gold]。\n本场战斗中此牌格挡值减少 1。",
        ["cards/BD_STREAMLINE.title"] = "精简改良",
        ["cards/BD_STREAMLINE.description"] = "造成 {Damage:diff()} 点伤害。\n本场战斗中每次打出后费用减少 1。",
        ["cards/BD_AGGREGATE.title"] = "汇集",
        ["cards/BD_AGGREGATE.description"] = "抽牌堆中每有 {Divisor:diff()} 张牌，获得 1 点[gold]能量[/gold]。",
        ["cards/BD_AUTO_SHIELDS.title"] = "自动护盾",
        ["cards/BD_AUTO_SHIELDS.description"] = "如果你没有[gold]格挡[/gold]，获得 {Block:diff()} 点[gold]格挡[/gold]。",
        ["cards/BD_BLIZZARD.title"] = "暴雪",
        ["cards/BD_BLIZZARD.description"] = "本场战斗每充能过 1 个[gold]冰霜[/gold]，对所有敌人造成 {Damage:diff()} 点伤害。",
        ["cards/BD_BULLSEYE.title"] = "瞄准靶心",
        ["cards/BD_BULLSEYE.description"] = "造成 {Damage:diff()} 点伤害。给予 {LockOn:diff()} 层[gold]锁定[/gold]。",
        ["cards/BD_CONSUME.title"] = "耗尽",
        ["cards/BD_CONSUME.description"] = "获得 {Focus:diff()} 点[gold]集中[/gold]。失去 1 个充能球栏位。",
        ["cards/BD_DOOM_AND_GLOOM.title"] = "愁云惨淡",
        ["cards/BD_DOOM_AND_GLOOM.description"] = "对所有敌人造成 {Damage:diff()} 点伤害。\n[gold]充能[/gold] 1 个[gold]黑暗[/gold]。",
        ["cards/BD_FORCE_FIELD.title"] = "力场",
        ["cards/BD_FORCE_FIELD.description"] = "获得 {Block:diff()} 点[gold]格挡[/gold]。\n本场战斗每打出 1 张能力牌，费用减少 1。",
        ["cards/BD_HEATSINKS.title"] = "散热片",
        ["cards/BD_HEATSINKS.description"] = "每当你打出一张能力牌，抽 {Draw:diff()} 张牌。",
        ["cards/BD_MELTER.title"] = "熔化",
        ["cards/BD_MELTER.description"] = "移除敌人的所有[gold]格挡[/gold]。\n造成 {Damage:diff()} 点伤害。",
        ["cards/BD_RECYCLE.title"] = "回收",
        ["cards/BD_RECYCLE.description"] = "选择并[gold]消耗[/gold] 1 张手牌，获得等同其费用的[gold]能量[/gold]。X 费牌按当前 X 值计算。",
        ["cards/BD_REINFORCED_BODY.title"] = "硬化机体",
        ["cards/BD_REINFORCED_BODY.description"] = "获得 {Block:diff()} 点[gold]格挡[/gold] X 次。",
        ["cards/BD_REPROGRAM.title"] = "重编程",
        ["cards/BD_REPROGRAM.description"] = "失去 {Focus:diff()} 点[gold]集中[/gold]。\n获得 {StrengthPower:diff()} 点[gold]力量[/gold]和 {DexterityPower:diff()} 点[gold]敏捷[/gold]。",
        ["cards/BD_SELF_REPAIR.title"] = "自我修复",
        ["cards/BD_SELF_REPAIR.description"] = "战斗结束时回复 {Heal:diff()} 点生命。",
        ["cards/BD_STATIC_DISCHARGE.title"] = "静电释放",
        ["cards/BD_STATIC_DISCHARGE.description"] = "每当你受到未被格挡的攻击伤害，[gold]充能[/gold] {Amount:diff()} 个[gold]闪电[/gold]。",
        ["cards/BD_SEEK.title"] = "搜寻",
        ["cards/BD_SEEK.description"] = "从你的抽牌堆中选择 {Amount:diff()} 张牌放入手牌。",
        ["cards/BD_SEEK.selectionScreenPrompt"] = "选择要放入手牌的牌",
        ["cards/BD_CORE_SURGE.title"] = "核心电涌",
        ["cards/BD_CORE_SURGE.description"] = "造成 {Damage:diff()} 点伤害。\n获得 1 层[gold]人工制品[/gold]。",
        ["cards/BD_ELECTRODYNAMICS.title"] = "电动力学",
        ["cards/BD_ELECTRODYNAMICS.description"] = "[gold]闪电[/gold]命中所有敌人。\n[gold]充能[/gold] {Amount:diff()} 个[gold]闪电[/gold]。",
        ["cards/BD_FISSION.title"] = "裂变",
        ["cards/BD_FISSION.description"] = "{IfUpgraded:show:[gold]激发[/gold]所有充能球。|移除所有充能球。}\n每处理 1 个充能球，获得 1 点[gold]能量[/gold]并抽 1 张牌。",
        ["cards/BD_THUNDER_STRIKE.title"] = "雷霆打击",
        ["cards/BD_THUNDER_STRIKE.description"] = "本场战斗每充能过 1 个[gold]闪电[/gold]，对随机敌人造成 {Damage:diff()} 点伤害。",

        ["powers/BD_HEATSINKS_POWER.title"] = "散热片",
        ["powers/BD_HEATSINKS_POWER.description"] = "每当你打出能力牌，抽 {Amount} 张牌。",
        ["powers/BD_HEATSINKS_POWER.smartDescription"] = "每当你打出能力牌，抽[blue]{Amount}[/blue]张牌。",
        ["powers/BD_SELF_REPAIR_POWER.title"] = "自我修复",
        ["powers/BD_SELF_REPAIR_POWER.description"] = "战斗结束时回复 {Amount} 点生命。",
        ["powers/BD_SELF_REPAIR_POWER.smartDescription"] = "战斗结束时回复[blue]{Amount}[/blue]点生命。",
        ["powers/BD_STATIC_DISCHARGE_POWER.title"] = "静电释放",
        ["powers/BD_STATIC_DISCHARGE_POWER.description"] = "每当你受到未被格挡的攻击伤害，充能 {Amount} 个闪电。",
        ["powers/BD_STATIC_DISCHARGE_POWER.smartDescription"] = "每当你受到未被格挡的攻击伤害，[gold]生成[/gold][blue]{Amount}[/blue]个[gold]闪电[/gold]充能球。",
        ["powers/BD_ELECTRODYNAMICS_POWER.title"] = "电动力学",
        ["powers/BD_ELECTRODYNAMICS_POWER.description"] = "闪电充能球的被动与激发伤害会命中所有敌人。",
        ["powers/BD_ELECTRODYNAMICS_POWER.smartDescription"] = "[gold]闪电[/gold]充能球的被动与激发伤害会命中所有敌人。",
        ["powers/BD_LOCK_ON_POWER.title"] = "锁定",
        ["powers/BD_LOCK_ON_POWER.description"] = "受到的充能球伤害提高 50%。每个敌人回合结束时减少 1 层。剩余 {Amount} 层。",
        ["powers/BD_LOCK_ON_POWER.smartDescription"] = "受到的充能球伤害提高[blue]50%[/blue]。每个敌人回合结束时减少1层。剩余[blue]{Amount}[/blue]层。",
    };

    public static bool TryGetRaw(string table, string key, out string raw) => Map.TryGetValue(table + "/" + key, out raw!);

    private static readonly Dictionary<string, string> CardsTable =
        Map.Where(kv => kv.Key.StartsWith("cards/", StringComparison.Ordinal))
           .ToDictionary(kv => kv.Key.Substring("cards/".Length), kv => kv.Value);

    private static readonly Dictionary<string, string> PowersTable =
        Map.Where(kv => kv.Key.StartsWith("powers/", StringComparison.Ordinal))
           .ToDictionary(kv => kv.Key.Substring("powers/".Length), kv => kv.Value);

    public static void MergeIntoLocManager()
    {
        try
        {
            var manager = LocManager.Instance;
            if (manager == null) return;

            MergeTable(manager, "cards", CardsTable);
            MergeTable(manager, "powers", PowersTable);
            RefreshVersionSensitiveCardDescriptions(manager);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to merge localization into LocManager: {ex}");
        }
    }

    /// <summary>
    /// Several v107 localization templates describe the current PC behavior,
    /// while BetterDefect can deliberately switch those cards back to an older
    /// implementation. Keep the visible card text on the same global version
    /// switch as the actual card code.
    /// </summary>
    public static void RefreshVersionSensitiveCardDescriptions()
    {
        try
        {
            var manager = LocManager.Instance;
            if (manager != null)
                RefreshVersionSensitiveCardDescriptions(manager);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to refresh version-sensitive descriptions: {ex.Message}");
        }
    }

    private static void RefreshVersionSensitiveCardDescriptions(LocManager manager)
    {
        var rocketV100 = IsVersionEnabled<RocketPunch>();
        var shatterV105 = IsVersionEnabled<Shatter>();
        var teslaV105 = IsVersionEnabled<TeslaCoil>();
        var compactV099 = IsVersionEnabled<Compact>();
        var scrapeV108 = IsVersionEnabled<Scrape>();
        var barrageCustom = IsVersionEnabled<Barrage>();
        var beamCellCustom = IsVersionEnabled<BeamCell>();
        var chargeBatteryCustom = IsVersionEnabled<ChargeBattery>();
        var coldSnapCustom = IsVersionEnabled<ColdSnap>();
        var coolheadedCustom = IsVersionEnabled<Coolheaded>();
        var goForTheEyesCustom = IsVersionEnabled<GoForTheEyes>();
        var gunkUpCustom = IsVersionEnabled<GunkUp>();
        var leapCustom = IsVersionEnabled<Leap>();
        var lightningRodCustom = IsVersionEnabled<LightningRod>();
        var sweepingBeamCustom = IsVersionEnabled<SweepingBeam>();
        var uproarCustom = IsVersionEnabled<Uproar>();
        var recursionCustom = IsVersionEnabled<Cards.BdRecursion>();
        var streamlineCustom = IsVersionEnabled<Cards.BdStreamline>();
        var chaosCustom = IsVersionEnabled<Chaos>();
        var doubleEnergyCustom = IsVersionEnabled<DoubleEnergy>();
        var fightThroughCustom = IsVersionEnabled<FightThrough>();
        var skimCustom = IsVersionEnabled<Skim>();
        var tempestCustom = IsVersionEnabled<Tempest>();
        var whiteNoiseCustom = IsVersionEnabled<WhiteNoise>();
        var ftlCustom = IsVersionEnabled<Ftl>();
        var nullCustom = IsVersionEnabled<Null>();
        var refractCustom = IsVersionEnabled<Refract>();
        var feralCustom = IsVersionEnabled<Feral>();
        var hailstormCustom = IsVersionEnabled<Hailstorm>();
        var iterationCustom = IsVersionEnabled<Iteration>();
        var loopCustom = IsVersionEnabled<Loop>();
        var smokestackCustom = IsVersionEnabled<Smokestack>();
        var stormCustom = IsVersionEnabled<Storm>();
        var subroutineCustom = IsVersionEnabled<Subroutine>();

        var descriptions = new Dictionary<string, string>
        {
            ["ROCKET_PUNCH.description"] = rocketV100
                ? "造成{Damage:diff()}点伤害。\n抽{Cards:diff()}张牌。\n每当你生成状态牌时，此牌的耗能将在下一次打出前降为0{energyPrefix:energyIcons(1)}。"
                : "造成{Damage:diff()}点伤害。\n抽{Cards:diff()}张牌。\n每当你生成状态牌时，此牌的耗能降为0{energyPrefix:energyIcons(1)}，直到打出或当前回合结束。",

            // Both the v0.108 baseline and selectable v0.105 behavior evoke
            // every orb twice. Android v103's stock text still says once.
            ["SHATTER.description"] = shatterV105
                ? "对所有敌人造成{Damage:diff()}点伤害。\n[gold]激发[/gold]所有充能球两次。"
                : "对所有敌人造成{Damage:diff()}点伤害。\n[gold]激发[/gold]所有充能球两次。",

            ["TESLA_COIL.description"] = teslaV105
                ? "造成{Damage:diff()}点伤害。\n对该敌人触发你的所有[gold]闪电[/gold]充能球被动{IfUpgraded:show:两次|一次}。"
                : "造成{Damage:diff()}点伤害。\n对该敌人触发你的所有[gold]闪电[/gold]充能球的被动一次。",

            ["FUEL.description"] = compactV099
                ? "获得{Energy:energyIcons()}。\n抽{Cards:diff()}张牌。"
                : "获得{Energy:energyIcons()}。",

            ["SCRAPE.description"] = scrapeV108
                ? "造成{Damage:diff()}点伤害。\n抽{Cards:diff()}张牌。\n按当前最终耗能计算，丢弃抽到的牌中耗能不为0{energyPrefix:energyIcons(1)}的牌。"
                : "造成{Damage:diff()}点伤害。\n抽{Cards:diff()}张牌。\n按卡牌自身耗能计算，丢弃抽到的牌中耗能不为0{energyPrefix:energyIcons(1)}的牌；由全局效果暂时降为0费的牌仍会被丢弃。",

            ["BARRAGE.description"] = barrageCustom
                ? "获得{Damage:diff()}点[gold]临时集中[/gold]，然后触发你的所有充能球的被动一次。"
                : "当前每有一个[gold]充能球[/gold]，造成{Damage:diff()}点伤害。{InCombat:\n（命中{CalculatedHits:diff()}次）|}",

            ["BEAM_CELL.description"] = beamCellCustom
                ? "造成{Damage:diff()}点伤害。\n给予{VulnerablePower:diff()}层[gold]锁定[/gold]。"
                : "造成{Damage:diff()}点伤害。\n给予{VulnerablePower:diff()}层[gold]易伤[/gold]。",

            ["CHARGE_BATTERY.description"] = chargeBatteryCustom
                ? "获得{Block:diff()}点[gold]格挡[/gold]。\n在下个回合获得{Energy:energyIcons()}并抽1张牌。"
                : "获得{Block:diff()}点[gold]格挡[/gold]。\n在下个回合获得{Energy:energyIcons()}。",

            ["COLD_SNAP.description"] = coldSnapCustom
                ? "造成{Damage:diff()}点伤害。\n[gold]生成[/gold]2个[gold]冰霜[/gold]充能球。"
                : "造成{Damage:diff()}点伤害。\n[gold]生成[/gold]1个[gold]冰霜[/gold]充能球。",

            ["COOLHEADED.description"] = coolheadedCustom
                ? "抽{Cards:diff()}张牌。\n[gold]生成[/gold]1个[gold]冰霜[/gold]充能球。"
                : "[gold]生成[/gold]1个[gold]冰霜[/gold]充能球。\n抽{Cards:diff()}张牌。",

            ["GO_FOR_THE_EYES.description"] = goForTheEyesCustom
                ? "造成{Damage:diff()}点伤害。\n给予{WeakPower:diff()}层[gold]虚弱[/gold]。"
                : "造成{Damage:diff()}点伤害。\n如果敌人的意图是攻击，则给予{WeakPower:diff()}层[gold]虚弱[/gold]。",

            ["GUNK_UP.description"] = gunkUpCustom
                ? "造成{Damage:diff()}点伤害{Repeat:diff()}次。\n在你的[gold]手牌[/gold]中加入一张[gold]黏液[/gold]。"
                : "造成{Damage:diff()}点伤害{Repeat:diff()}次。\n在你的[gold]弃牌堆[/gold]中加入一张[gold]黏液[/gold]。",

            ["LEAP.description"] = leapCustom
                ? "获得{Block:diff()}点[gold]格挡[/gold]。\n本场战斗中此牌耗能变为0{energyPrefix:energyIcons(1)}。"
                : "获得{Block:diff()}点[gold]格挡[/gold]。",

            ["LIGHTNING_ROD.description"] = lightningRodCustom
                ? "获得{Block:diff()}点[gold]格挡[/gold]。\n立即[gold]生成[/gold]1个[gold]闪电[/gold]；下回合开始时再生成1个。"
                : "获得{Block:diff()}点[gold]格挡[/gold]。\n在下{LightningRodPower:diff()}个回合开始时，[gold]生成[/gold]1个[gold]闪电[/gold]充能球。",

            ["SWEEPING_BEAM.description"] = sweepingBeamCustom
                ? "对所有敌人造成{Damage:diff()}点伤害。\n抽{Cards:diff()}张牌。"
                : "对所有敌人造成{Damage:diff()}点伤害。\n抽1张牌。",

            ["UPROAR.description"] = uproarCustom
                ? "造成{Damage:diff()}点伤害两次。\n优先随机打出你的[gold]抽牌堆[/gold]中的1张当前为2费的攻击牌；若没有，则随机打出1张攻击牌。"
                : "造成{Damage:diff()}点伤害两次。\n随机打出你的[gold]抽牌堆[/gold]中的1张攻击牌。",

            ["BD_RECURSION.description"] = recursionCustom
                ? "[gold]激发[/gold]最左侧充能球两次，然后重新充能同类型充能球。"
                : "[gold]激发[/gold]最左侧充能球一次，然后重新充能同类型充能球。",

            ["BD_STREAMLINE.description"] = streamlineCustom
                ? "造成{Damage:diff()}点伤害。\n每次打出后，使本场战斗中所有[gold]精简改良[/gold]的费用减少1。"
                : "造成{Damage:diff()}点伤害。\n本场战斗中每次打出后费用减少1。",

            ["CHAOS.description"] = chaosCustom
                ? "[gold]生成[/gold]{Repeat:diff()}个随机充能球。优先生成当前充能球栏位中没有的种类。"
                : "[gold]生成[/gold]{Repeat:diff()}个随机充能球。",

            ["DOUBLE_ENERGY.description"] = doubleEnergyCustom
                ? "将你的能量翻倍。\n抽1张牌。"
                : "将你的能量翻倍。",

            ["FIGHT_THROUGH.description"] = fightThroughCustom
                ? "获得{Block:diff()}点[gold]格挡[/gold]。\n将2张[gold]眩晕[/gold]加入你的[gold]弃牌堆[/gold]。"
                : "获得{Block:diff()}点[gold]格挡[/gold]。\n将2张[gold]伤口[/gold]加入你的[gold]弃牌堆[/gold]。",

            ["SKIM.description"] = skimCustom
                ? "丢弃1张牌。\n抽{Cards:diff()}张牌。"
                : "抽{Cards:diff()}张牌。",

            ["TEMPEST.description"] = tempestCustom
                ? "[gold]生成[/gold]{IfUpgraded:show:X+1|X}个[gold]闪电[/gold]充能球。\n每因此[gold]激发[/gold]1个[gold]闪电[/gold]充能球，抽1张牌。"
                : "[gold]生成[/gold]{IfUpgraded:show:X+1|X}个[gold]闪电[/gold]充能球。",

            ["WHITE_NOISE.description"] = whiteNoiseCustom
                ? "从3张随机机器人能力牌中选择1张加入你的[gold]手牌[/gold]。这张牌在本回合内免费打出。"
                : "将一张随机能力牌加入你的[gold]手牌[/gold]。这张牌在本回合内免费打出。",

            ["FTL.description"] = ftlCustom
                ? "造成{Damage:diff()}点伤害。\n如果你在这回合打出的牌数小于{PlayMax:diff()}张，抽1张牌；否则给予{IfUpgraded:show:2|1}层[gold]锁定[/gold]。"
                : "造成{Damage:diff()}点伤害。\n如果你在这回合打出的牌数小于{PlayMax:diff()}张，抽1张牌。",

            ["NULL.description"] = nullCustom
                ? "造成{Damage:diff()}点伤害。\n给予{WeakPower:diff()}层[gold]虚弱[/gold]。\n[gold]生成[/gold]1个[gold]黑暗[/gold]充能球。若目标在打出前已有[gold]虚弱[/gold]，再生成1个。"
                : "造成{Damage:diff()}点伤害。\n给予{WeakPower:diff()}层[gold]虚弱[/gold]。\n[gold]生成[/gold]1个[gold]黑暗[/gold]充能球。",

            ["REFRACT.description"] = refractCustom
                ? "造成{Damage:diff()}点伤害两次。\n[gold]生成[/gold]{Repeat:diff()}个[gold]玻璃[/gold]充能球。若充能球栏位中有[gold]玻璃[/gold]，此牌耗能变为2。"
                : "造成{Damage:diff()}点伤害两次。\n[gold]生成[/gold]{Repeat:diff()}个[gold]玻璃[/gold]充能球。",

            ["FERAL.description"] = feralCustom
                ? "你每回合第一次打出的耗能为0{energyPrefix:energyIcons(1)}的牌，会放回你的[gold]手牌[/gold]。"
                : "你每回合打出的{FeralPower:choose(1):第一张|前{FeralPower:diff()}张}\n耗能为0{energyPrefix:energyIcons(1)}的攻击牌，\n会放回你的[gold]手牌[/gold]。",

            ["HAILSTORM.description"] = hailstormCustom
                ? "在你的回合结束时，每有1个[gold]冰霜[/gold]充能球，就对所有敌人造成{HailstormPower:diff()}点伤害。"
                : "在你的回合结束时，如果你有[gold]冰霜[/gold]充能球，则对所有敌人造成{HailstormPower:diff()}点伤害。",

            ["ITERATION.description"] = iterationCustom
                ? "每回合你第一次抽到状态牌时，抽{IterationPower:diff()}张牌，然后[gold]消耗[/gold]该状态牌。"
                : "每回合你第一次抽到状态牌时，抽{IterationPower:diff()}张牌。",

            ["LOOP.description"] = loopCustom
                ? "在你的回合开始时，分别触发最左侧和最右侧充能球的被动一次。同一个充能球只触发一次。"
                : "在你的回合开始时，触发你最右侧的一个充能球的被动能力{IfUpgraded:show:2次}。",

            ["SMOKESTACK.description"] = smokestackCustom
                ? "每当你生成一张状态牌时，对所有敌人造成{SmokestackPower:diff()}点伤害。每回合第一次触发时，额外抽1张牌。"
                : "每当你生成一张状态牌时，对所有敌人造成{SmokestackPower:diff()}点伤害。",

            ["STORM.description"] = stormCustom
                ? "[gold]固有[/gold]。\n每当你打出一张能力牌时，[gold]生成[/gold]{StormPower:diff()}个[gold]闪电[/gold]充能球。"
                : "每当你打出一张能力牌时，[gold]生成[/gold]{StormPower:diff()}个[gold]闪电[/gold]充能球。",

            ["SUBROUTINE.description"] = subroutineCustom
                ? "当你打出一张能力牌时，获得{energyPrefix:energyIcons(1)}。每回合第一次触发时，额外抽1张牌。"
                : "当你打出一张能力牌时，获得{energyPrefix:energyIcons(1)}。",
        };

        manager.GetTable("cards").MergeWith(descriptions);
    }

    private static bool IsVersionEnabled<T>() where T : CardModel
    {
        try { return BdCardVersionUpgrades.IsVersionEnabled(ModelDb.Card<T>()); }
        catch { return false; }
    }

    private static void MergeTable(LocManager manager, string tableName, Dictionary<string, string> entries)
    {
        if (entries.Count == 0) return;

        try
        {
            manager.GetTable(tableName).MergeWith(entries);
            MainFile.Logger.Info($"[BetterDefect] merged {entries.Count} {tableName} localization entries into LocManager.");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to merge {tableName} localization entries: {ex.Message}");
        }
    }
}
