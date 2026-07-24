using System.Reflection;
using System.Collections;
using System.Runtime.CompilerServices;
using BetterDefect.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using BufferCard = MegaCrit.Sts2.Core.Models.Cards.Buffer;

namespace BetterDefect;

/// <summary>
/// The 35-point card-version upgrade system.  A point does not put a normal
/// smithing upgrade on every copy of a card; it switches that card to the
/// requested historical balance version, while normal + upgrades continue to
/// work on individual cards.
/// </summary>
internal static class BdCardVersionUpgrades
{
    private static readonly Type[] VersionedCardTypes =
    [
        typeof(Hotfix), typeof(RocketPunch), typeof(Voltaic), typeof(Hyperbeam),
        typeof(Shatter), typeof(TeslaCoil), typeof(Uproar), typeof(Fusion),
        typeof(Synthesis), typeof(Compact), typeof(MomentumStrike), typeof(Scrape),
        typeof(Sunder), typeof(TrashToTreasure),

        // Custom common-card transformations selected by the user. These use
        // the same persistent 35-point system as historical versions, but are
        // deliberately labelled as custom transformations in the Encyclopedia.
        typeof(Barrage), typeof(BeamCell), typeof(ChargeBattery), typeof(ColdSnap), typeof(Coolheaded),
        typeof(GoForTheEyes), typeof(GunkUp), typeof(Leap), typeof(LightningRod),
        typeof(SweepingBeam), typeof(BdRecursion), typeof(BdStreamline),

        // User-approved uncommon-card transformations.
        typeof(Chaos), typeof(DoubleEnergy), typeof(FightThrough), typeof(Skim),
        typeof(Tempest), typeof(WhiteNoise), typeof(Ftl), typeof(Null),
        typeof(Refract), typeof(Feral), typeof(Hailstorm), typeof(Iteration),
        typeof(Loop), typeof(Smokestack), typeof(Storm), typeof(Subroutine),

        // User-approved rare-card transformations.
        typeof(AdaptiveStrike), typeof(AllForOne), typeof(BufferCard), typeof(ConsumingShadow),
        typeof(Coolant), typeof(CreativeAi), typeof(EchoForm), typeof(FlakCannon),
        typeof(GeneticAlgorithm), typeof(IceLance), typeof(Defragment), typeof(BiasedCognition),
        typeof(MeteorStrike), typeof(MultiCast), typeof(Rainbow), typeof(BdThunderStrike),
        typeof(BdCoreSurge)
    ];

    private static readonly Type[] CustomUpgradeTypes = [.. VersionedCardTypes, typeof(Fuel)];
    private static readonly HashSet<Type> VersionedCardTypeSet = new(VersionedCardTypes);
    private static readonly Dictionary<string, (string Version, string Effect)> VersionedCardSpecs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CARD.HOTFIX"] = ("v0.99", "基础牌不再消耗；普通升级额外把集中从2提高到3"),
            ["CARD.ROCKET_PUNCH"] = ("v0.100", "生成状态牌后，0费持续到这张牌被打出，而不是只持续到回合结束"),
            ["CARD.VOLTAIC"] = ("v0.99", "耗能从3降为2；普通升级仍会移除消耗"),
            ["CARD.HYPERBEAM"] = ("v0.109", "伤害由26(34)提高到30(38)"),
            ["CARD.SHATTER"] = ("v0.105", "伤害由7(11)提高到11(15)，移除消耗；所有充能球仍激发两次"),
            ["CARD.TESLA_COIL"] = ("v0.105", "基础：3伤并触发所有闪电被动1次；升级：4伤并触发2次"),
            ["CARD.UPROAR"] = ("改造：自定义", "每段伤害保持5(7)；优先自动打出抽牌堆中的2费攻击牌"),
            ["CARD.FUSION"] = ("v0.106", "耗能由2(1)改为1；基础牌消耗，普通升级移除消耗"),
            ["CARD.SYNTHESIS"] = ("v0.106", "伤害由12(18)提高到14(20)"),
            ["CARD.COMPACT"] = ("v0.99", "生成的燃料由获得1(2)能量改为获得1能量并抽1(2)张牌"),
            ["CARD.MOMENTUM_STRIKE"] = ("v0.108", "伤害由10(13)提高到11(15)"),
            ["CARD.SCRAPE"] = ("v0.108", "正确保留被全局临时效果降为0费的牌"),
            ["CARD.SUNDER"] = ("v0.109", "伤害由24(32)提高到26(34)"),
            ["CARD.TRASH_TO_TREASURE"] = ("v0.99", "普通升级由耗能降为0改为获得固有"),
            ["CARD.BARRAGE"] = ("改造：自定义", "获得2(3)点临时集中，然后触发所有充能球的被动一次"),
            ["CARD.BEAM_CELL"] = ("改造：自定义", "0费造成3(4)点伤害并施加1(2)层锁定，不再施加易伤"),
            ["CARD.CHARGE_BATTERY"] = ("改造：自定义", "1费获得6(9)格挡；下回合获得1能量并抽1张牌"),
            ["CARD.COLD_SNAP"] = ("改造：自定义", "2费造成12(18)伤害并生成2个冰霜"),
            ["CARD.COOLHEADED"] = ("改造：自定义", "1费抽2张牌并生成1个冰霜；基础牌消耗，普通升级移除消耗"),
            ["CARD.GO_FOR_THE_EYES"] = ("改造：自定义", "造成3(4)伤害；无论敌人意图都施加1(2)层虚弱"),
            ["CARD.GUNK_UP"] = ("改造：自定义", "造成4(5)伤害三次；黏液加入手牌而不是弃牌堆"),
            ["CARD.LEAP"] = ("改造：自定义", "获得8(11)格挡；打出后本场战斗变为0费"),
            ["CARD.LIGHTNING_ROD"] = ("改造：自定义", "获得5(6)格挡；立即生成1闪电，下回合再生成1闪电"),
            ["CARD.SWEEPING_BEAM"] = ("改造：自定义", "对所有敌人造成6(9)伤害并抽1(2)张牌"),
            ["CARD.BD_RECURSION"] = ("改造：自定义", "激发最左侧充能球两次并重新生成；普通升级改为0费"),
            ["CARD.BD_STREAMLINE"] = ("改造：自定义", "造成13(18)伤害；每次打出使所有精简改良本场战斗少1费"),
            ["CARD.CHAOS"] = ("改造：自定义", "1费生成2个随机充能球，优先生成当前栏位中没有的种类；基础牌消耗，普通升级移除消耗"),
            ["CARD.DOUBLE_ENERGY"] = ("改造：自定义", "1(0)费消耗；将当前能量翻倍并抽1张牌"),
            ["CARD.FIGHT_THROUGH"] = ("改造：自定义", "1费获得12(18)格挡，将2张眩晕加入弃牌堆"),
            ["CARD.SKIM"] = ("改造：自定义", "1费先弃1张手牌，再抽3(4)张牌"),
            ["CARD.TEMPEST"] = ("改造：自定义", "生成X(X+1)个闪电；每因球位溢出激发一个闪电球便抽1张牌"),
            ["CARD.WHITE_NOISE"] = ("改造：自定义", "1(0)费消耗；从3张随机机器人能力牌中选择1张，本回合免费"),
            ["CARD.FTL"] = ("改造：自定义", "0费造成5(7)伤害；前三(四)张牌内抽1张，否则施加1(2)层锁定"),
            ["CARD.NULL"] = ("改造：自定义", "2费造成10(13)伤害，施加2(3)层虚弱并生成黑暗；目标原有虚弱则再生成1个"),
            ["CARD.REFRACT"] = ("改造：自定义", "3费造成9(12)伤害两次并生成2个玻璃；栏位中有玻璃球时变为2费"),
            ["CARD.FERAL"] = ("改造：自定义", "2(1)费；每回合第一次打出的0费牌返回手牌"),
            ["CARD.HAILSTORM"] = ("改造：自定义", "1费；回合结束时每有1个冰霜，对所有敌人造成2(3)伤害"),
            ["CARD.ITERATION"] = ("改造：自定义", "1费；每回合首次抽到状态牌时抽2(3)张牌，然后消耗该状态牌"),
            ["CARD.LOOP"] = ("改造：自定义", "1(0)费；回合开始时分别触发最左侧与最右侧充能球被动一次"),
            ["CARD.SMOKESTACK"] = ("改造：自定义", "1费；每生成状态牌对全体造成4(6)伤害，每回合首次触发额外抽1张"),
            ["CARD.STORM"] = ("改造：自定义", "1费固有；每打出能力牌生成1(2)个闪电球"),
            ["CARD.SUBROUTINE"] = ("改造：自定义", "1(0)费；打出能力牌获得1能量，每回合首次触发额外抽1张"),
            ["CARD.ADAPTIVE_STRIKE"] = ("改造：自定义", "2费造成16(22)伤害；将一张0费、虚无的复制品加入抽牌堆"),
            ["CARD.ALL_FOR_ONE"] = ("改造：自定义", "1费造成6(9)伤害；从弃牌堆选择至多2(3)张当前为0费的牌加入手牌"),
            ["CARD.BUFFER"] = ("改造：自定义", "2费获得1(2)层缓冲和10点格挡"),
            ["CARD.CONSUMING_SHADOW"] = ("改造：自定义", "生成1(2)个黑暗；回合结束时触发所有黑暗球被动一次"),
            ["CARD.COOLANT"] = ("改造：自定义", "回合结束时，每有一种不同充能球获得2(3)格挡"),
            ["CARD.CREATIVE_AI"] = ("改造：自定义", "每回合从3张随机机器人能力牌中选择1张，本回合免费"),
            ["CARD.ECHO_FORM"] = ("改造：自定义", "每回合第2张牌额外打出一次；升级移除虚无"),
            ["CARD.FLAK_CANNON"] = ("改造：自定义", "消耗所有未被消耗的状态牌；消耗牌堆中每有一张牌，对指定敌人造成6(9)伤害"),
            ["CARD.GENETIC_ALGORITHM"] = ("改造：自定义", "稀有度改为蓝卡，效果不变"),
            ["CARD.ICE_LANCE"] = ("改造：自定义", "稀有度改为蓝卡，效果不变"),
            ["CARD.DEFRAGMENT"] = ("改造：自定义", "稀有度改为蓝卡，效果不变"),
            ["CARD.BIASED_COGNITION"] = ("改造：自定义", "稀有度改为金卡，效果不变"),
            ["CARD.METEOR_STRIKE"] = ("改造：自定义", "4费造成18(24)伤害并生成2个等离子"),
            ["CARD.MULTI_CAST"] = ("改造：自定义", "激发X(X+1)个充能球，并在每次激发后重新生成相同类型的球"),
            ["CARD.RAINBOW"] = ("改造：自定义", "3费依次生成闪电、冰霜、黑暗、玻璃、等离子；基础消耗，升级移除消耗"),
            ["CARD.BD_THUNDER_STRIKE"] = ("改造：自定义", "效果不变；普通升级后费用改为2"),
            ["CARD.BD_CORE_SURGE"] = ("改造：自定义", "效果不变；普通升级后获得固有")
        };
    private static readonly FieldInfo? EnergyBaseField = AccessTools.Field(typeof(CardEnergyCost), "_base");
    private static readonly FieldInfo? KeywordsField = AccessTools.Field(typeof(CardModel), "_keywords");
    private static readonly FieldInfo? TargetTypeField = AccessTools.Field(typeof(CardModel), "<TargetType>k__BackingField");

    internal static IEnumerable<Type> UpgradeMethodTypes => CustomUpgradeTypes;

    internal static bool IsEligible(CardModel? card)
    {
        if (card == null) return false;
        if (VersionedCardTypeSet.Contains(card.GetType())) return true;
        return VersionedCardSpecs.ContainsKey(SafeCardId(card));
    }

    internal static string GetTargetVersionLabel(CardModel card) => card switch
    {
        Hotfix => "v0.99",
        RocketPunch => "v0.100",
        Voltaic => "v0.99",
        Hyperbeam => "v0.109",
        Shatter => "v0.105",
        TeslaCoil => "v0.105",
        Uproar => "改造：自定义",
        Fusion => "v0.106",
        Synthesis => "v0.106",
        Compact => "v0.99",
        MomentumStrike => "v0.108",
        Scrape => "v0.108",
        Sunder => "v0.109",
        TrashToTreasure => "v0.99",
        Barrage or BeamCell or ChargeBattery or ColdSnap or Coolheaded or GoForTheEyes or GunkUp or Leap or LightningRod or SweepingBeam or BdRecursion or BdStreamline or
        Chaos or DoubleEnergy or FightThrough or Skim or Tempest or WhiteNoise or Ftl or Null or Refract or Feral or Hailstorm or Iteration or Loop or Smokestack or Storm or Subroutine or
        AdaptiveStrike or AllForOne or BufferCard or ConsumingShadow or Coolant or CreativeAi or EchoForm or FlakCannon or GeneticAlgorithm or IceLance or Defragment or BiasedCognition or
        MeteorStrike or MultiCast or Rainbow or BdThunderStrike or BdCoreSurge => "改造：自定义",
        _ => VersionedCardSpecs.TryGetValue(SafeCardId(card), out var spec) ? spec.Version : "历史版本"
    };

    internal static string GetTargetEffectSummary(CardModel card) => card switch
    {
        Hotfix => "基础牌不再消耗；普通升级额外把集中从2提高到3",
        RocketPunch => "生成状态牌后，0费持续到这张牌被打出，而不是只持续到回合结束",
        Voltaic => "耗能从3降为2；普通升级仍会移除消耗",
        Hyperbeam => "伤害由26(34)提高到30(38)",
        Shatter => "伤害由7(11)提高到11(15)，移除消耗；所有充能球仍激发两次",
        TeslaCoil => "基础：3伤并触发所有闪电被动1次；升级：4伤并触发2次",
        Uproar => "每段伤害保持5(7)；优先自动打出抽牌堆中的2费攻击牌",
        Fusion => "耗能由2(1)改为1；基础牌消耗，普通升级移除消耗",
        Synthesis => "伤害由12(18)提高到14(20)",
        Compact => "生成的燃料由获得1(2)能量改为获得1能量并抽1(2)张牌",
        MomentumStrike => "伤害由10(13)提高到11(15)",
        Scrape => "正确保留被全局临时效果降为0费的牌",
        Sunder => "伤害由24(32)提高到26(34)",
        TrashToTreasure => "普通升级由耗能降为0改为获得固有",
        Barrage => "获得2(3)点临时集中，然后触发所有充能球的被动一次",
        BeamCell => "0费造成3(4)点伤害并施加1(2)层锁定，不再施加易伤",
        ChargeBattery => "1费获得6(9)格挡；下回合获得1能量并抽1张牌",
        ColdSnap => "2费造成12(18)伤害并生成2个冰霜",
        Coolheaded => "1费抽2张牌并生成1个冰霜；基础牌消耗，普通升级移除消耗",
        GoForTheEyes => "造成3(4)伤害；无论敌人意图都施加1(2)层虚弱",
        GunkUp => "造成4(5)伤害三次；黏液加入手牌而不是弃牌堆",
        Leap => "获得8(11)格挡；打出后本场战斗变为0费",
        LightningRod => "获得5(6)格挡；立即生成1闪电，下回合再生成1闪电",
        SweepingBeam => "对所有敌人造成6(9)伤害并抽1(2)张牌",
        BdRecursion => "激发最左侧充能球两次并重新生成；普通升级改为0费",
        BdStreamline => "造成13(18)伤害；每次打出使所有精简改良本场战斗少1费",
        Chaos => "1费生成1(2)个随机充能球，优先生成当前栏位中没有的种类",
        DoubleEnergy => "1(0)费消耗；将当前能量翻倍并抽1张牌",
        FightThrough => "1费获得12(18)格挡，将2张眩晕加入弃牌堆",
        Skim => "1费先弃1张手牌，再抽3(4)张牌",
        Tempest => "生成X(X+1)个闪电；每因球位溢出激发一个闪电球便抽1张牌",
        WhiteNoise => "1(0)费消耗；从3张随机机器人能力牌中选择1张，本回合免费",
        Ftl => "0费造成5(7)伤害；前三(四)张牌内抽1张，否则施加1(2)层锁定",
        Null => "2费造成10(13)伤害，施加2(3)层虚弱并生成黑暗；目标原有虚弱则再生成1个",
        Refract => "3费造成9(12)伤害两次并生成2个玻璃；栏位中有玻璃球时变为2费",
        Feral => "2(1)费；每回合第一次打出的0费牌返回手牌",
        Hailstorm => "1费；回合结束时每有1个冰霜，对所有敌人造成2(3)伤害",
        Iteration => "1费；每回合首次抽到状态牌时抽2(3)张牌，然后消耗该状态牌",
        Loop => "1(0)费；回合开始时分别触发最左侧与最右侧充能球被动一次",
        Smokestack => "1费；每生成状态牌对全体造成4(6)伤害，每回合首次触发额外抽1张",
        Storm => "1费固有；每打出能力牌生成1(2)个闪电球",
        Subroutine => "1(0)费；打出能力牌获得1能量，每回合首次触发额外抽1张",
        AdaptiveStrike => "2费造成16(22)伤害；将一张0费、虚无的复制品加入抽牌堆",
        AllForOne => "1费造成6(9)伤害；从弃牌堆选择至多2(3)张当前为0费的牌加入手牌",
        BufferCard => "2费获得1(2)层缓冲和10点格挡",
        ConsumingShadow => "生成1(2)个黑暗；回合结束时触发所有黑暗球被动一次",
        Coolant => "回合结束时，每有一种不同充能球获得2(3)格挡",
        CreativeAi => "每回合从3张随机机器人能力牌中选择1张，本回合免费",
        EchoForm => "每回合第2张牌额外打出一次；普通升级移除虚无",
        FlakCannon => "消耗所有未被消耗的状态牌；消耗牌堆中每有一张牌，对指定敌人造成6(9)伤害",
        GeneticAlgorithm => "改为蓝卡，效果不变",
        IceLance => "改为蓝卡，效果不变",
        Defragment => "改为蓝卡，效果不变",
        BiasedCognition => "改为金卡，效果不变",
        MeteorStrike => "4费造成18(24)伤害并生成2个等离子",
        MultiCast => "激发X(X+1)个充能球，并在每次激发后重新生成相同类型的球",
        Rainbow => "3费依次生成闪电、冰霜、黑暗、玻璃、等离子；基础消耗，升级移除消耗",
        BdThunderStrike => "效果不变；普通升级后费用改为2",
        BdCoreSurge => "效果不变；普通升级后获得固有",
        _ => VersionedCardSpecs.TryGetValue(SafeCardId(card), out var spec) ? spec.Effect : "切换到指定历史版本"
    };

    private static string SafeCardId(CardModel card)
    {
        try { return card.Id.ToString(); }
        catch { return string.Empty; }
    }

    internal static void RefreshAllCanonicalModels()
    {
        try
        {
            foreach (var card in ModelDb.AllCards)
                ApplyToModel(card);
            BdLocalization.RefreshVersionSensitiveCardDescriptions();
            MainFile.Logger.Info($"[BetterDefect] card-version baselines applied; upgrades={BdDynamicOdds.GetVersionUpgradeCount()}, points={BdDynamicOdds.GetUsedCardPointCount()}/{BdDynamicOdds.MaxCardPointBudget}.");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to refresh historical card versions: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void RefreshCanonicalFor(CardModel card)
    {
        try
        {
            foreach (var model in ModelDb.AllCards.Where(c => c.GetType() == card.GetType()))
                ApplyToModel(model);

            if (card is Compact)
            {
                foreach (var fuel in ModelDb.AllCards.OfType<Fuel>())
                    ApplyToModel(fuel);
            }
            BdLocalization.RefreshVersionSensitiveCardDescriptions();
        }
        catch { }
    }

    internal static void ApplyToModel(CardModel? card)
    {
        if (card == null) return;

        var upgradedVersion = IsVersionEnabled(card);
        var plus = card.IsUpgraded;

        switch (card)
        {
            case Barrage:
                // Barrage already owns a Damage DynamicVar, so reuse it as
                // the visible temporary-Focus amount while transformed.
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 3m : 2m : plus ? 7m : 5m);
                SetTargetType(card, upgradedVersion ? TargetType.None : TargetType.AnyEnemy);
                break;

            case BeamCell:
                SetDynamic(card, "Damage", plus ? 4m : 3m);
                // Reuse the vanilla Vulnerable DynamicVar as the visible
                // 1(2) amount while the custom play route applies Lock-On.
                SetDynamic(card, "VulnerablePower", plus ? 2m : 1m);
                break;

            case ChargeBattery:
                SetDynamic(card, "Block", upgradedVersion
                    ? plus ? 9m : 6m
                    : plus ? 10m : 7m);
                break;

            case ColdSnap:
                SetEnergy(card, upgradedVersion ? 2 : 1);
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 18m : 12m
                    : plus ? 9m : 6m);
                break;

            case Coolheaded:
                SetEnergy(card, 1);
                SetDynamic(card, "Cards", upgradedVersion ? 2m : plus ? 2m : 1m);
                SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus);
                break;

            case GoForTheEyes:
                SetDynamic(card, "Damage", plus ? 4m : 3m);
                SetDynamic(card, "WeakPower", plus ? 2m : 1m);
                break;

            case GunkUp:
                SetDynamic(card, "Damage", plus ? 5m : 4m);
                break;

            case Leap:
                SetDynamic(card, "Block", upgradedVersion
                    ? plus ? 11m : 8m
                    : plus ? 12m : 9m);
                break;

            case LightningRod:
                SetDynamic(card, "Block", upgradedVersion
                    ? plus ? 6m : 5m
                    : plus ? 7m : 4m);
                SetDynamic(card, "LightningRodPower", upgradedVersion ? 1m : 2m);
                break;

            case SweepingBeam:
                SetDynamic(card, "Damage", plus ? 9m : 6m);
                SetDynamic(card, "Cards", upgradedVersion && plus ? 2m : 1m);
                break;

            case Chaos:
                SetEnergy(card, 1);
                SetDynamic(card, "Repeat", upgradedVersion ? 2m : plus ? 2m : 1m);
                SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus);
                break;

            case DoubleEnergy:
                SetEnergy(card, plus ? 0 : 1);
                SetKeyword(card, CardKeyword.Exhaust, true);
                break;

            case FightThrough:
                SetDynamic(card, "Block", upgradedVersion
                    ? plus ? 18m : 12m
                    : plus ? 17m : 13m);
                break;

            case Skim:
                SetDynamic(card, "Cards", plus ? 4m : 3m);
                break;

            case WhiteNoise:
                SetEnergy(card, plus ? 0 : 1);
                SetKeyword(card, CardKeyword.Exhaust, true);
                break;

            case Ftl:
                SetDynamic(card, "Damage", plus ? (upgradedVersion ? 7m : 6m) : 5m);
                SetDynamic(card, "PlayMax", plus ? 4m : 3m);
                break;

            case Null:
                SetDynamic(card, "Damage", plus ? 13m : 10m);
                SetDynamic(card, "WeakPower", plus ? 3m : 2m);
                break;

            case Refract:
                SetEnergy(card, 3);
                SetDynamic(card, "Damage", plus ? 12m : 9m);
                SetDynamic(card, "Repeat", 2m);
                break;

            case Feral:
                SetEnergy(card, plus ? 1 : 2);
                SetDynamic(card, "FeralPower", 1m);
                break;

            case Hailstorm:
                SetDynamic(card, "HailstormPower", upgradedVersion
                    ? plus ? 3m : 2m
                    : plus ? 8m : 6m);
                break;

            case Iteration:
                SetDynamic(card, "IterationPower", plus ? 3m : 2m);
                break;

            case Loop:
                SetEnergy(card, upgradedVersion && plus ? 0 : 1);
                SetDynamic(card, "Loop", upgradedVersion ? 1m : plus ? 2m : 1m);
                break;

            case Smokestack:
                SetDynamic(card, "SmokestackPower", upgradedVersion
                    ? plus ? 6m : 4m
                    : plus ? 7m : 5m);
                break;

            case Storm:
                SetDynamic(card, "StormPower", plus ? 2m : 1m);
                SetKeyword(card, CardKeyword.Innate, upgradedVersion);
                break;

            case Subroutine:
                SetEnergy(card, plus ? 0 : 1);
                break;

            case AdaptiveStrike:
                SetEnergy(card, 2);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 22m : 16m : plus ? 23m : 18m);
                break;

            case AllForOne:
                SetEnergy(card, upgradedVersion ? 1 : 2);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 9m : 6m : plus ? 14m : 10m);
                break;

            case BufferCard:
                SetEnergy(card, 2);
                SetDynamic(card, "BufferPower", plus ? 2m : 1m);
                break;

            case ConsumingShadow:
                SetEnergy(card, 2);
                SetDynamic(card, "Repeat", upgradedVersion ? plus ? 2m : 1m : plus ? 3m : 2m);
                break;

            case Coolant:
                SetEnergy(card, 1);
                SetDynamic(card, "CoolantPower", plus ? 3m : 2m);
                break;

            case CreativeAi:
                SetEnergy(card, plus ? 2 : 3);
                break;

            case EchoForm:
                SetEnergy(card, 3);
                SetKeyword(card, CardKeyword.Ethereal, !plus);
                break;

            case FlakCannon:
                SetEnergy(card, 2);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 9m : 6m : plus ? 11m : 8m);
                SetTargetType(card, upgradedVersion ? TargetType.AnyEnemy : TargetType.RandomEnemy);
                break;

            case MeteorStrike:
                SetEnergy(card, upgradedVersion ? 4 : 5);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 24m : 18m : plus ? 30m : 24m);
                break;

            case Rainbow:
                SetEnergy(card, upgradedVersion ? 3 : 2);
                SetKeyword(card, CardKeyword.Exhaust, !plus);
                break;

            case BdThunderStrike:
                SetEnergy(card, upgradedVersion && plus ? 2 : 3);
                SetDynamic(card, "Damage", plus ? 9m : 7m);
                break;

            case BdCoreSurge:
                SetEnergy(card, 1);
                SetDynamic(card, "Damage", plus ? 15m : 11m);
                SetKeyword(card, CardKeyword.Exhaust, true);
                SetKeyword(card, CardKeyword.Innate, upgradedVersion && plus);
                break;

            case Hotfix:
                SetDynamic(card, "FocusPower", plus && upgradedVersion ? 3m : 2m);
                // v0.99 had no Exhaust at either normal-upgrade level.  The
                // v0.103 baseline only removes Exhaust on a normal + upgrade.
                SetKeyword(card, CardKeyword.Exhaust, !upgradedVersion && !plus);
                break;

            case RocketPunch:
                SetDynamic(card, "Damage", plus ? 14m : 13m);
                SetDynamic(card, "Cards", plus ? 2m : 1m);
                break;

            case Voltaic:
                SetEnergy(card, upgradedVersion ? 2 : 3);
                SetKeyword(card, CardKeyword.Exhaust, !plus);
                break;

            case Hyperbeam:
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 38m : 30m
                    : plus ? 34m : 26m);
                break;

            case Shatter:
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 15m : 11m
                    : plus ? 11m : 7m);
                SetKeyword(card, CardKeyword.Exhaust, !upgradedVersion);
                break;

            case TeslaCoil:
                SetDynamic(card, "Damage", plus
                    ? upgradedVersion ? 4m : 6m
                    : 3m);
                break;

            case Uproar:
                SetDynamic(card, "Damage", plus ? 7m : 5m);
                break;

            case Fusion:
                SetEnergy(card, upgradedVersion ? 1 : plus ? 1 : 2);
                SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus);
                break;

            case Synthesis:
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 20m : 14m
                    : plus ? 18m : 12m);
                break;

            case Compact:
                SetDynamic(card, "Block", plus ? 7m : 6m);
                break;

            case Fuel:
                ApplyFuelVersion(card, plus, IsCompactVersionEnabled());
                break;

            case MomentumStrike:
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 15m : 11m
                    : plus ? 13m : 10m);
                break;

            case Scrape:
                SetDynamic(card, "Damage", plus ? 10m : 7m);
                SetDynamic(card, "Cards", plus ? 5m : 4m);
                break;

            case Sunder:
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 34m : 26m
                    : plus ? 32m : 24m);
                break;

            case TrashToTreasure:
                SetEnergy(card, plus && !upgradedVersion ? 0 : 1);
                SetKeyword(card, CardKeyword.Innate, plus && upgradedVersion);
                break;

            case BdRecursion:
                SetEnergy(card, plus ? 0 : 1);
                break;

            case BdStreamline:
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 18m : 13m
                    : plus ? 20m : 15m);
                break;
        }

        // Encyclopedia upgrade previews may use a card model loaded through a
        // different assembly identity on some desktop/mobile builds. In that
        // case the C# type-pattern switch above does not match even though the
        // stable card ID is correct. Apply the same balance values by ID as a
        // fallback so the button and the actual effect cannot diverge.
        if (!VersionedCardTypeSet.Contains(card.GetType()) && card is not Fuel)
            ApplyVersionById(card, upgradedVersion, plus);
    }

    internal static void ApplyNormalUpgrade(CardModel card)
    {
        var upgradedVersion = IsVersionEnabled(card);

        switch (card)
        {
            case Barrage:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 3m : 7m);
                break;
            case BeamCell:
                UpgradeDynamicTo(card, "Damage", 4m);
                UpgradeDynamicTo(card, "VulnerablePower", 2m);
                break;
            case ChargeBattery:
                UpgradeDynamicTo(card, "Block", upgradedVersion ? 9m : 10m);
                break;
            case ColdSnap:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 18m : 9m);
                break;
            case Coolheaded:
                UpgradeDynamicTo(card, "Cards", 2m);
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case GoForTheEyes:
                UpgradeDynamicTo(card, "Damage", 4m);
                UpgradeDynamicTo(card, "WeakPower", 2m);
                break;
            case GunkUp:
                UpgradeDynamicTo(card, "Damage", 5m);
                break;
            case Leap:
                UpgradeDynamicTo(card, "Block", upgradedVersion ? 11m : 12m);
                break;
            case LightningRod:
                UpgradeDynamicTo(card, "Block", upgradedVersion ? 6m : 7m);
                break;
            case SweepingBeam:
                UpgradeDynamicTo(card, "Damage", 9m);
                UpgradeDynamicTo(card, "Cards", upgradedVersion ? 2m : 1m);
                break;
            case Chaos:
                UpgradeDynamicTo(card, "Repeat", 2m);
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case DoubleEnergy:
                SetEnergy(card, 0);
                break;
            case FightThrough:
                UpgradeDynamicTo(card, "Block", upgradedVersion ? 18m : 17m);
                break;
            case Skim:
                UpgradeDynamicTo(card, "Cards", 4m);
                break;
            case WhiteNoise:
                SetEnergy(card, 0);
                break;
            case Ftl:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 7m : 6m);
                UpgradeDynamicTo(card, "PlayMax", 4m);
                break;
            case Null:
                UpgradeDynamicTo(card, "Damage", 13m);
                UpgradeDynamicTo(card, "WeakPower", 3m);
                break;
            case Refract:
                UpgradeDynamicTo(card, "Damage", 12m);
                break;
            case Feral:
                SetEnergy(card, 1);
                UpgradeDynamicTo(card, "FeralPower", 1m);
                break;
            case Hailstorm:
                UpgradeDynamicTo(card, "HailstormPower", upgradedVersion ? 3m : 8m);
                break;
            case Iteration:
                UpgradeDynamicTo(card, "IterationPower", 3m);
                break;
            case Loop:
                if (upgradedVersion) SetEnergy(card, 0);
                else UpgradeDynamicTo(card, "Loop", 2m);
                break;
            case Smokestack:
                UpgradeDynamicTo(card, "SmokestackPower", upgradedVersion ? 6m : 7m);
                break;
            case Storm:
                UpgradeDynamicTo(card, "StormPower", 2m);
                SetKeyword(card, CardKeyword.Innate, upgradedVersion);
                break;
            case Subroutine:
                SetEnergy(card, 0);
                break;
            case AdaptiveStrike:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 22m : 23m);
                break;
            case AllForOne:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 9m : 14m);
                break;
            case BufferCard:
                UpgradeDynamicTo(card, "BufferPower", 2m);
                break;
            case ConsumingShadow:
                UpgradeDynamicTo(card, "Repeat", upgradedVersion ? 2m : 3m);
                break;
            case Coolant:
                UpgradeDynamicTo(card, "CoolantPower", 3m);
                break;
            case CreativeAi:
                SetEnergy(card, 2);
                break;
            case EchoForm:
                SetKeyword(card, CardKeyword.Ethereal, false);
                break;
            case FlakCannon:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 9m : 11m);
                break;
            case MeteorStrike:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 24m : 30m);
                break;
            case Rainbow:
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case BdThunderStrike:
                UpgradeDynamicTo(card, "Damage", 9m);
                if (upgradedVersion) SetEnergy(card, 2);
                break;
            case BdCoreSurge:
                UpgradeDynamicTo(card, "Damage", 15m);
                if (upgradedVersion) SetKeyword(card, CardKeyword.Innate, true);
                break;
            case Hotfix:
                if (upgradedVersion) UpgradeDynamicTo(card, "FocusPower", 3m);
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case RocketPunch:
                UpgradeDynamicTo(card, "Damage", 14m);
                UpgradeDynamicTo(card, "Cards", 2m);
                break;
            case Voltaic:
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case Hyperbeam:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 38m : 34m);
                break;
            case Shatter:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 15m : 11m);
                break;
            case TeslaCoil:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 4m : 6m);
                break;
            case Uproar:
                UpgradeDynamicTo(card, "Damage", 7m);
                break;
            case Fusion:
                if (upgradedVersion)
                    SetKeyword(card, CardKeyword.Exhaust, false);
                else
                    card.EnergyCost.UpgradeBy(-1);
                break;
            case Synthesis:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 20m : 18m);
                break;
            case Compact:
                UpgradeDynamicTo(card, "Block", 7m);
                break;
            case Fuel:
                if (IsCompactVersionEnabled())
                    UpgradeDynamicTo(card, "Cards", 2m);
                else
                    UpgradeDynamicTo(card, "Energy", 2m);
                break;
            case MomentumStrike:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 15m : 13m);
                break;
            case Scrape:
                UpgradeDynamicTo(card, "Damage", 10m);
                UpgradeDynamicTo(card, "Cards", 5m);
                break;
            case Sunder:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 34m : 32m);
                break;
            case TrashToTreasure:
                if (upgradedVersion)
                    SetKeyword(card, CardKeyword.Innate, true);
                else
                    card.EnergyCost.UpgradeBy(-1);
                break;
            case BdRecursion:
                SetEnergy(card, 0);
                break;
            case BdStreamline:
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 18m : 20m);
                break;
        }


        if (!VersionedCardTypeSet.Contains(card.GetType()) && card is not Fuel)
            ApplyNormalUpgradeById(card, upgradedVersion);
    }

    private static void ApplyVersionById(CardModel card, bool upgradedVersion, bool plus)
    {
        switch (SafeCardId(card).ToUpperInvariant())
        {
            case "CARD.BARRAGE":
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 3m : 2m : plus ? 7m : 5m);
                SetTargetType(card, upgradedVersion ? TargetType.None : TargetType.AnyEnemy);
                break;
            case "CARD.BEAM_CELL":
                SetDynamic(card, "Damage", plus ? 4m : 3m);
                SetDynamic(card, "VulnerablePower", plus ? 2m : 1m);
                break;
            case "CARD.CHARGE_BATTERY":
                SetDynamic(card, "Block", upgradedVersion ? plus ? 9m : 6m : plus ? 10m : 7m);
                break;
            case "CARD.COLD_SNAP":
                SetEnergy(card, upgradedVersion ? 2 : 1);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 18m : 12m : plus ? 9m : 6m);
                break;
            case "CARD.COOLHEADED":
                SetEnergy(card, 1);
                SetDynamic(card, "Cards", upgradedVersion ? 2m : plus ? 2m : 1m);
                SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus);
                break;
            case "CARD.GO_FOR_THE_EYES":
                SetDynamic(card, "Damage", plus ? 4m : 3m);
                SetDynamic(card, "WeakPower", plus ? 2m : 1m);
                break;
            case "CARD.GUNK_UP": SetDynamic(card, "Damage", plus ? 5m : 4m); break;
            case "CARD.LEAP":
                SetDynamic(card, "Block", upgradedVersion ? plus ? 11m : 8m : plus ? 12m : 9m);
                break;
            case "CARD.LIGHTNING_ROD":
                SetDynamic(card, "Block", upgradedVersion ? plus ? 6m : 5m : plus ? 7m : 4m);
                SetDynamic(card, "LightningRodPower", upgradedVersion ? 1m : 2m);
                break;
            case "CARD.SWEEPING_BEAM":
                SetDynamic(card, "Damage", plus ? 9m : 6m);
                SetDynamic(card, "Cards", upgradedVersion && plus ? 2m : 1m);
                break;
            case "CARD.CHAOS":
                SetEnergy(card, 1);
                SetDynamic(card, "Repeat", upgradedVersion ? 2m : plus ? 2m : 1m);
                SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus);
                break;
            case "CARD.DOUBLE_ENERGY":
                SetEnergy(card, plus ? 0 : 1);
                SetKeyword(card, CardKeyword.Exhaust, true);
                break;
            case "CARD.FIGHT_THROUGH":
                SetDynamic(card, "Block", upgradedVersion ? plus ? 18m : 12m : plus ? 17m : 13m);
                break;
            case "CARD.SKIM": SetDynamic(card, "Cards", plus ? 4m : 3m); break;
            case "CARD.WHITE_NOISE":
                SetEnergy(card, plus ? 0 : 1);
                SetKeyword(card, CardKeyword.Exhaust, true);
                break;
            case "CARD.FTL":
                SetDynamic(card, "Damage", plus ? upgradedVersion ? 7m : 6m : 5m);
                SetDynamic(card, "PlayMax", plus ? 4m : 3m);
                break;
            case "CARD.NULL":
                SetDynamic(card, "Damage", plus ? 13m : 10m);
                SetDynamic(card, "WeakPower", plus ? 3m : 2m);
                break;
            case "CARD.REFRACT":
                SetEnergy(card, 3);
                SetDynamic(card, "Damage", plus ? 12m : 9m);
                SetDynamic(card, "Repeat", 2m);
                break;
            case "CARD.FERAL":
                SetEnergy(card, plus ? 1 : 2);
                SetDynamic(card, "FeralPower", 1m);
                break;
            case "CARD.HAILSTORM":
                SetDynamic(card, "HailstormPower", upgradedVersion ? plus ? 3m : 2m : plus ? 8m : 6m);
                break;
            case "CARD.ITERATION": SetDynamic(card, "IterationPower", plus ? 3m : 2m); break;
            case "CARD.LOOP":
                SetEnergy(card, upgradedVersion && plus ? 0 : 1);
                SetDynamic(card, "Loop", upgradedVersion ? 1m : plus ? 2m : 1m);
                break;
            case "CARD.SMOKESTACK":
                SetDynamic(card, "SmokestackPower", upgradedVersion ? plus ? 6m : 4m : plus ? 7m : 5m);
                break;
            case "CARD.STORM":
                SetDynamic(card, "StormPower", plus ? 2m : 1m);
                SetKeyword(card, CardKeyword.Innate, upgradedVersion);
                break;
            case "CARD.SUBROUTINE": SetEnergy(card, plus ? 0 : 1); break;
            case "CARD.ADAPTIVE_STRIKE":
                SetEnergy(card, 2);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 22m : 16m : plus ? 23m : 18m);
                break;
            case "CARD.ALL_FOR_ONE":
                SetEnergy(card, upgradedVersion ? 1 : 2);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 9m : 6m : plus ? 14m : 10m);
                break;
            case "CARD.BUFFER":
                SetEnergy(card, 2);
                SetDynamic(card, "BufferPower", plus ? 2m : 1m);
                break;
            case "CARD.CONSUMING_SHADOW":
                SetEnergy(card, 2);
                SetDynamic(card, "Repeat", upgradedVersion ? plus ? 2m : 1m : plus ? 3m : 2m);
                break;
            case "CARD.COOLANT":
                SetEnergy(card, 1);
                SetDynamic(card, "CoolantPower", plus ? 3m : 2m);
                break;
            case "CARD.CREATIVE_AI": SetEnergy(card, plus ? 2 : 3); break;
            case "CARD.ECHO_FORM":
                SetEnergy(card, 3);
                SetKeyword(card, CardKeyword.Ethereal, !plus);
                break;
            case "CARD.FLAK_CANNON":
                SetEnergy(card, 2);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 9m : 6m : plus ? 11m : 8m);
                SetTargetType(card, upgradedVersion ? TargetType.AnyEnemy : TargetType.RandomEnemy);
                break;
            case "CARD.METEOR_STRIKE":
                SetEnergy(card, upgradedVersion ? 4 : 5);
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 24m : 18m : plus ? 30m : 24m);
                break;
            case "CARD.RAINBOW":
                SetEnergy(card, upgradedVersion ? 3 : 2);
                SetKeyword(card, CardKeyword.Exhaust, !plus);
                break;
            case "CARD.BD_THUNDER_STRIKE":
                SetEnergy(card, upgradedVersion && plus ? 2 : 3);
                SetDynamic(card, "Damage", plus ? 9m : 7m);
                break;
            case "CARD.BD_CORE_SURGE":
                SetEnergy(card, 1);
                SetDynamic(card, "Damage", plus ? 15m : 11m);
                SetKeyword(card, CardKeyword.Exhaust, true);
                SetKeyword(card, CardKeyword.Innate, upgradedVersion && plus);
                break;
            case "CARD.HOTFIX":
                SetDynamic(card, "FocusPower", plus && upgradedVersion ? 3m : 2m);
                SetKeyword(card, CardKeyword.Exhaust, !upgradedVersion && !plus);
                break;
            case "CARD.ROCKET_PUNCH":
                SetDynamic(card, "Damage", plus ? 14m : 13m);
                SetDynamic(card, "Cards", plus ? 2m : 1m);
                break;
            case "CARD.VOLTAIC":
                SetEnergy(card, upgradedVersion ? 2 : 3);
                SetKeyword(card, CardKeyword.Exhaust, !plus);
                break;
            case "CARD.HYPERBEAM":
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 38m : 30m : plus ? 34m : 26m);
                break;
            case "CARD.SHATTER":
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 15m : 11m : plus ? 11m : 7m);
                SetKeyword(card, CardKeyword.Exhaust, !upgradedVersion);
                break;
            case "CARD.TESLA_COIL":
                SetDynamic(card, "Damage", plus ? upgradedVersion ? 4m : 6m : 3m);
                break;
            case "CARD.UPROAR":
                SetDynamic(card, "Damage", plus ? 7m : 5m);
                break;
            case "CARD.FUSION":
                SetEnergy(card, upgradedVersion ? 1 : plus ? 1 : 2);
                SetKeyword(card, CardKeyword.Exhaust, upgradedVersion && !plus);
                break;
            case "CARD.SYNTHESIS":
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 20m : 14m : plus ? 18m : 12m);
                break;
            case "CARD.COMPACT":
                SetDynamic(card, "Block", plus ? 7m : 6m);
                break;
            case "CARD.MOMENTUM_STRIKE":
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 15m : 11m : plus ? 13m : 10m);
                break;
            case "CARD.SCRAPE":
                SetDynamic(card, "Damage", plus ? 10m : 7m);
                SetDynamic(card, "Cards", plus ? 5m : 4m);
                break;
            case "CARD.SUNDER":
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 34m : 26m : plus ? 32m : 24m);
                break;
            case "CARD.TRASH_TO_TREASURE":
                SetEnergy(card, plus && !upgradedVersion ? 0 : 1);
                SetKeyword(card, CardKeyword.Innate, plus && upgradedVersion);
                break;
            case "CARD.BD_RECURSION": SetEnergy(card, plus ? 0 : 1); break;
            case "CARD.BD_STREAMLINE":
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 18m : 13m : plus ? 20m : 15m);
                break;
        }
    }

    private static void ApplyNormalUpgradeById(CardModel card, bool upgradedVersion)
    {
        switch (SafeCardId(card).ToUpperInvariant())
        {
            case "CARD.BARRAGE": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 3m : 7m); break;
            case "CARD.BEAM_CELL":
                UpgradeDynamicTo(card, "Damage", 4m);
                UpgradeDynamicTo(card, "VulnerablePower", 2m);
                break;
            case "CARD.CHARGE_BATTERY": UpgradeDynamicTo(card, "Block", upgradedVersion ? 9m : 10m); break;
            case "CARD.COLD_SNAP": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 18m : 9m); break;
            case "CARD.COOLHEADED":
                UpgradeDynamicTo(card, "Cards", 2m);
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case "CARD.GO_FOR_THE_EYES":
                UpgradeDynamicTo(card, "Damage", 4m);
                UpgradeDynamicTo(card, "WeakPower", 2m);
                break;
            case "CARD.GUNK_UP": UpgradeDynamicTo(card, "Damage", 5m); break;
            case "CARD.LEAP": UpgradeDynamicTo(card, "Block", upgradedVersion ? 11m : 12m); break;
            case "CARD.LIGHTNING_ROD": UpgradeDynamicTo(card, "Block", upgradedVersion ? 6m : 7m); break;
            case "CARD.SWEEPING_BEAM":
                UpgradeDynamicTo(card, "Damage", 9m);
                UpgradeDynamicTo(card, "Cards", upgradedVersion ? 2m : 1m);
                break;
            case "CARD.CHAOS":
                UpgradeDynamicTo(card, "Repeat", 2m);
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case "CARD.DOUBLE_ENERGY": SetEnergy(card, 0); break;
            case "CARD.FIGHT_THROUGH": UpgradeDynamicTo(card, "Block", upgradedVersion ? 18m : 17m); break;
            case "CARD.SKIM": UpgradeDynamicTo(card, "Cards", 4m); break;
            case "CARD.WHITE_NOISE": SetEnergy(card, 0); break;
            case "CARD.FTL":
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 7m : 6m);
                UpgradeDynamicTo(card, "PlayMax", 4m);
                break;
            case "CARD.NULL":
                UpgradeDynamicTo(card, "Damage", 13m);
                UpgradeDynamicTo(card, "WeakPower", 3m);
                break;
            case "CARD.REFRACT": UpgradeDynamicTo(card, "Damage", 12m); break;
            case "CARD.FERAL":
                SetEnergy(card, 1);
                UpgradeDynamicTo(card, "FeralPower", 1m);
                break;
            case "CARD.HAILSTORM": UpgradeDynamicTo(card, "HailstormPower", upgradedVersion ? 3m : 8m); break;
            case "CARD.ITERATION": UpgradeDynamicTo(card, "IterationPower", 3m); break;
            case "CARD.LOOP":
                if (upgradedVersion) SetEnergy(card, 0);
                else UpgradeDynamicTo(card, "Loop", 2m);
                break;
            case "CARD.SMOKESTACK": UpgradeDynamicTo(card, "SmokestackPower", upgradedVersion ? 6m : 7m); break;
            case "CARD.STORM":
                UpgradeDynamicTo(card, "StormPower", 2m);
                SetKeyword(card, CardKeyword.Innate, upgradedVersion);
                break;
            case "CARD.SUBROUTINE": SetEnergy(card, 0); break;
            case "CARD.ADAPTIVE_STRIKE": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 22m : 23m); break;
            case "CARD.ALL_FOR_ONE": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 9m : 14m); break;
            case "CARD.BUFFER": UpgradeDynamicTo(card, "BufferPower", 2m); break;
            case "CARD.CONSUMING_SHADOW": UpgradeDynamicTo(card, "Repeat", upgradedVersion ? 2m : 3m); break;
            case "CARD.COOLANT": UpgradeDynamicTo(card, "CoolantPower", 3m); break;
            case "CARD.CREATIVE_AI": SetEnergy(card, 2); break;
            case "CARD.ECHO_FORM": SetKeyword(card, CardKeyword.Ethereal, false); break;
            case "CARD.FLAK_CANNON": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 9m : 11m); break;
            case "CARD.METEOR_STRIKE": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 24m : 30m); break;
            case "CARD.RAINBOW": SetKeyword(card, CardKeyword.Exhaust, false); break;
            case "CARD.BD_THUNDER_STRIKE":
                UpgradeDynamicTo(card, "Damage", 9m);
                if (upgradedVersion) SetEnergy(card, 2);
                break;
            case "CARD.BD_CORE_SURGE":
                UpgradeDynamicTo(card, "Damage", 15m);
                if (upgradedVersion) SetKeyword(card, CardKeyword.Innate, true);
                break;
            case "CARD.HOTFIX":
                if (upgradedVersion) UpgradeDynamicTo(card, "FocusPower", 3m);
                SetKeyword(card, CardKeyword.Exhaust, false);
                break;
            case "CARD.ROCKET_PUNCH":
                UpgradeDynamicTo(card, "Damage", 14m);
                UpgradeDynamicTo(card, "Cards", 2m);
                break;
            case "CARD.VOLTAIC": SetKeyword(card, CardKeyword.Exhaust, false); break;
            case "CARD.HYPERBEAM": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 38m : 34m); break;
            case "CARD.SHATTER": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 15m : 11m); break;
            case "CARD.TESLA_COIL": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 4m : 6m); break;
            case "CARD.UPROAR": UpgradeDynamicTo(card, "Damage", 7m); break;
            case "CARD.FUSION":
                if (upgradedVersion) SetKeyword(card, CardKeyword.Exhaust, false);
                else card.EnergyCost.UpgradeBy(-1);
                break;
            case "CARD.SYNTHESIS": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 20m : 18m); break;
            case "CARD.COMPACT": UpgradeDynamicTo(card, "Block", 7m); break;
            case "CARD.MOMENTUM_STRIKE": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 15m : 13m); break;
            case "CARD.SCRAPE":
                UpgradeDynamicTo(card, "Damage", 10m);
                UpgradeDynamicTo(card, "Cards", 5m);
                break;
            case "CARD.SUNDER": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 34m : 32m); break;
            case "CARD.TRASH_TO_TREASURE":
                if (upgradedVersion) SetKeyword(card, CardKeyword.Innate, true);
                else card.EnergyCost.UpgradeBy(-1);
                break;
            case "CARD.BD_RECURSION": SetEnergy(card, 0); break;
            case "CARD.BD_STREAMLINE": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 18m : 20m); break;
        }
    }

    internal static bool IsVersionEnabled(CardModel card) => IsEligible(card) && BdDynamicOdds.IsCardVersionUpgraded(card);

    internal static bool IsVersionEnabled<T>() where T : CardModel
    {
        try { return IsVersionEnabled(ModelDb.Card<T>()); }
        catch { return false; }
    }

    internal static bool TryGetTransformedRarity(CardModel card, out CardRarity rarity)
    {
        rarity = default;
        if (!IsVersionEnabled(card)) return false;
        switch (card)
        {
            case GeneticAlgorithm:
            case IceLance:
            case Defragment:
                rarity = CardRarity.Uncommon;
                return true;
            case BiasedCognition:
                rarity = CardRarity.Rare;
                return true;
            default:
                return false;
        }
    }

    private static bool IsCompactVersionEnabled()
    {
        try { return BdDynamicOdds.IsCardVersionUpgraded(ModelDb.AllCards.OfType<Compact>().FirstOrDefault()); }
        catch { return false; }
    }

    private static void ApplyFuelVersion(CardModel card, bool plus, bool oldCompactEnabled)
    {
        if (oldCompactEnabled)
        {
            SetDynamic(card, "Energy", 1m);
            SetDynamic(card, "Cards", plus ? 2m : 1m);
        }
        else
        {
            SetDynamic(card, "Energy", plus ? 2m : 1m);
            SetDynamic(card, "Cards", 0m);
        }
    }

    private static void SetDynamic(CardModel card, string name, decimal value)
    {
        try { card.DynamicVars[name].BaseValue = value; }
        catch { }
    }

    private static void UpgradeDynamicTo(CardModel card, string name, decimal value)
    {
        try
        {
            var variable = card.DynamicVars[name];
            variable.UpgradeValueBy(value - variable.BaseValue);
        }
        catch { }
    }

    private static void SetEnergy(CardModel card, int value)
    {
        try
        {
            var cost = card.EnergyCost;
            if (EnergyBaseField != null)
                EnergyBaseField.SetValue(cost, value);
            else if (card.IsMutable)
                cost.SetCustomBaseCost(value);
            if (card.IsMutable)
                card.InvokeEnergyCostChanged();
        }
        catch { }
    }

    private static void SetTargetType(CardModel card, TargetType value)
    {
        try { TargetTypeField?.SetValue(card, value); }
        catch { }
    }

    private static void SetKeyword(CardModel card, CardKeyword keyword, bool enabled)
    {
        try
        {
            var has = card.Keywords.Contains(keyword);
            if (has == enabled) return;

            if (card.IsMutable)
            {
                if (enabled) card.AddKeyword(keyword);
                else card.RemoveKeyword(keyword);
                return;
            }

            _ = card.Keywords;
            if (KeywordsField?.GetValue(card) is HashSet<CardKeyword> keywords)
            {
                if (enabled) keywords.Add(keyword);
                else keywords.Remove(keyword);
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
internal static class BdCardVersionModelDbInitPatch
{
    private static void Postfix() => BdCardVersionUpgrades.RefreshAllCanonicalModels();
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.ToMutable))]
internal static class BdCardVersionToMutablePatch
{
    private static void Postfix(ref CardModel __result) => BdCardVersionUpgrades.ApplyToModel(__result);
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.DowngradeInternal))]
internal static class BdCardVersionDowngradePatch
{
    private static void Postfix(CardModel __instance) => BdCardVersionUpgrades.ApplyToModel(__instance);
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpgradeInternal))]
internal static class BdCardVersionNormalUpgradePatch
{
    // One base upgrade pipeline hook replaces fifteen per-card OnUpgrade
    // detours. Let the game perform its normal upgrade first, then normalize
    // the final values to the selected historical version. Besides producing
    // the same result, this greatly reduces ARM64 MonoMod trampoline pressure
    // during Android startup.
    private static void Postfix(CardModel __instance) =>
        BdCardVersionUpgrades.ApplyToModel(__instance);
}

/// <summary>
/// Custom common-card play routes. A single Harmony patch class owns the nine
/// affected vanilla OnPlay methods so Android performs one class-discovery
/// pass, while each method still falls straight through to vanilla whenever
/// its persistent Encyclopedia transformation is disabled.
/// </summary>
[HarmonyPatch]
internal static class BdCustomCommonCardPlayPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type[] types =
        [
            typeof(Barrage), typeof(BeamCell), typeof(ChargeBattery), typeof(ColdSnap), typeof(Coolheaded),
            typeof(GoForTheEyes), typeof(GunkUp), typeof(Leap), typeof(LightningRod),
            typeof(Uproar), typeof(Chaos), typeof(DoubleEnergy), typeof(FightThrough),
            typeof(Skim), typeof(Tempest), typeof(WhiteNoise), typeof(Ftl), typeof(Null)
        ];
        foreach (var type in types)
        {
            var method = AccessTools.DeclaredMethod(type, "OnPlay");
            if (method != null) yield return method;
        }
    }

    private static bool Prefix(
        CardModel __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance))
            return true;

        __result = __instance switch
        {
            Barrage card => PlayBarrage(card, choiceContext),
            BeamCell card => PlayBeamCell(card, choiceContext, cardPlay),
            ChargeBattery card => PlayChargeBattery(card, choiceContext, cardPlay),
            ColdSnap card => PlayColdSnap(card, choiceContext, cardPlay),
            Coolheaded card => PlayCoolheaded(card, choiceContext),
            GoForTheEyes card => PlayGoForTheEyes(card, choiceContext, cardPlay),
            GunkUp card => PlayGunkUp(card, choiceContext, cardPlay),
            Leap card => PlayLeap(card, cardPlay),
            LightningRod card => PlayLightningRod(card, choiceContext, cardPlay),
            Uproar card => PlayUproar(card, choiceContext, cardPlay),
            Chaos card => PlayChaos(card, choiceContext),
            DoubleEnergy card => PlayDoubleEnergy(card, choiceContext),
            FightThrough card => PlayFightThrough(card, choiceContext, cardPlay),
            Skim card => PlaySkim(card, choiceContext),
            Tempest card => PlayTempest(card, choiceContext),
            WhiteNoise card => PlayWhiteNoise(card, choiceContext),
            Ftl card => PlayFtl(card, choiceContext, cardPlay),
            Null card => PlayNull(card, choiceContext, cardPlay),
            _ => Task.CompletedTask
        };
        return false;
    }

    private static async Task PlayBarrage(Barrage card, PlayerChoiceContext choiceContext)
    {
        var temporaryFocus = card.DynamicVars.Damage.BaseValue;
        var focusApplied = false;
        // Snapshot the queue before resolving passives. Every orb present when
        // Barrage was played triggers once, in visible left-to-right order,
        // while the temporary Focus is active.
        var orbs = card.Owner.PlayerCombatState.OrbQueue.Orbs.ToList();
        try
        {
            await Bd.ApplyPower<FocusPower>(
                choiceContext,
                card.Owner.Creature,
                temporaryFocus,
                card.Owner.Creature,
                card);
            focusApplied = true;
            foreach (var orb in orbs)
                await OrbCmd.Passive(choiceContext, orb, null);
        }
        finally
        {
            if (focusApplied)
            {
                // Remove the temporary amount directly instead of applying
                // negative Focus, so Artifact cannot turn it into permanent
                // Focus. ModifyPowerAmount also preserves any Focus that was
                // already present before Barrage was played.
                var focus = card.Owner.Creature.GetPower<FocusPower>();
                if (focus != null)
                    await Bd.ModifyPowerAmount(
                        choiceContext,
                        focus,
                        -temporaryFocus,
                        card.Owner.Creature,
                        card,
                        silent: true);
            }
        }
    }

    private static async Task PlayBeamCell(BeamCell card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
        await Bd.ApplyPower<BdLockOnPower>(
            choiceContext,
            cardPlay.Target,
            card.DynamicVars.Vulnerable.BaseValue,
            card.Owner.Creature,
            card);
    }

    private static async Task PlayChargeBattery(ChargeBattery card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, cardPlay);
        await Bd.ApplyPower<EnergyNextTurnPower>(choiceContext, card.Owner.Creature, 1m, card.Owner.Creature, card);
        await Bd.ApplyPower<DrawCardsNextTurnPower>(choiceContext, card.Owner.Creature, 1m, card.Owner.Creature, card);
    }

    private static async Task PlayColdSnap(ColdSnap card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
        await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);
        await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);
    }

    private static async Task PlayCoolheaded(Coolheaded card, PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        await CardPileCmd.Draw(choiceContext, card.DynamicVars.Cards.BaseValue, card.Owner);
        await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);
    }

    private static async Task PlayGoForTheEyes(GoForTheEyes card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
        await Bd.ApplyPower<WeakPower>(choiceContext, cardPlay.Target, card.DynamicVars.Weak.BaseValue, card.Owner.Creature, card);
    }

    private static async Task PlayGunkUp(GunkUp card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).WithHitCount(card.DynamicVars.Repeat.IntValue).FromCard(card)
            .Targeting(cardPlay.Target)
            .WithHitFx(null, null, "blunt_attack.mp3")
            .WithHitVfxNode(NGoopyImpactVfx.Create)
            .Execute(choiceContext);
        var slimed = Bd.CreateCard<Slimed>(card);
        CardCmd.PreviewCardPileAdd(await Bd.AddGeneratedCardToCombat(slimed, PileType.Hand, card.Owner));
        await Cmd.Wait(0.5f);
    }

    private static async Task PlayLeap(Leap card, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, cardPlay);
        card.EnergyCost.SetThisCombat(0);
    }

    private static async Task PlayLightningRod(LightningRod card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, cardPlay);
        await OrbCmd.Channel<LightningOrb>(choiceContext, card.Owner);
        await Bd.ApplyPower<LightningRodPower>(choiceContext, card.Owner.Creature, 1m, card.Owner.Creature, card);
    }

    private static async Task PlayUproar(Uproar card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).WithHitCount(2)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var playableAttacks = PileType.Draw.GetPile(card.Owner).Cards
            .Where(c => c.Type == CardType.Attack && !c.Keywords.Contains(CardKeyword.Unplayable))
            .ToList();
        var preferred = playableAttacks.Where(IsCurrentTwoCost).ToList();
        var candidates = preferred.Count > 0 ? preferred : playableAttacks;
        var selected = candidates.StableShuffle(card.Owner.RunState.Rng.Shuffle).FirstOrDefault();

        // Preserve vanilla's defensive fallback for unusual modded attacks
        // carrying Unplayable while still preferring playable 2-cost attacks.
        selected ??= PileType.Draw.GetPile(card.Owner).Cards
            .Where(c => c.Type == CardType.Attack)
            .ToList()
            .StableShuffle(card.Owner.RunState.Rng.Shuffle)
            .FirstOrDefault();
        if (selected != null)
            await CardCmd.AutoPlay(choiceContext, selected, null);
    }

    private static async Task PlayChaos(Chaos card, PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        for (var i = 0; i < card.DynamicVars.Repeat.IntValue; i++)
        {
            var occupiedTypes = card.Owner.PlayerCombatState.OrbQueue.Orbs
                .Select(orb => orb.GetType())
                .ToHashSet();
            var canonical = ModelDb.Orbs.ToList();
            var missing = canonical.Where(orb => !occupiedTypes.Contains(orb.GetType())).ToList();
            var pool = missing.Count > 0 ? missing : canonical;
            var selected = pool.StableShuffle(card.Owner.RunState.Rng.CombatOrbGeneration).FirstOrDefault();
            if (selected != null)
                await OrbCmd.Channel(choiceContext, selected.ToMutable(), card.Owner);
        }
    }

    private static async Task PlayDoubleEnergy(DoubleEnergy card, PlayerChoiceContext choiceContext)
    {
        await PlayerCmd.GainEnergy(card.Owner.PlayerCombatState.Energy, card.Owner);
        await CardPileCmd.Draw(choiceContext, 1m, card.Owner);
    }

    private static async Task PlayFightThrough(FightThrough card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, cardPlay);
        for (var i = 0; i < 2; i++)
        {
            var dazed = Bd.CreateCard<Dazed>(card);
            CardCmd.PreviewCardPileAdd(await Bd.AddGeneratedCardToCombat(dazed, PileType.Discard, card.Owner));
        }
    }

    private static async Task PlaySkim(Skim card, PlayerChoiceContext choiceContext)
    {
        var selected = await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            card.Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
            null,
            card);
        await CardCmd.Discard(choiceContext, selected);
        await CardPileCmd.Draw(choiceContext, card.DynamicVars.Cards.BaseValue, card.Owner);
    }

    private static async Task PlayTempest(Tempest card, PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        var count = card.ResolveEnergyXValue() + (card.IsUpgraded ? 1 : 0);
        for (var i = 0; i < count; i++)
        {
            var queue = card.Owner.PlayerCombatState.OrbQueue;
            var evokedLightning = queue.Capacity > 0
                                  && queue.Orbs.Count >= queue.Capacity
                                  && queue.Orbs.FirstOrDefault() is LightningOrb;
            await OrbCmd.Channel<LightningOrb>(choiceContext, card.Owner);
            if (evokedLightning)
                await CardPileCmd.Draw(choiceContext, 1m, card.Owner);
        }
    }

    private static async Task PlayWhiteNoise(WhiteNoise card, PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        var choices = CardFactory.GetDistinctForCombat(
                card.Owner,
                card.Owner.Character.CardPool.GetUnlockedCards(card.Owner.UnlockState, card.Owner.RunState.CardMultiplayerConstraint)
                    .Where(candidate => candidate.Type == CardType.Power),
                3,
                card.Owner.RunState.Rng.CombatCardGeneration)
            .ToList();
        var selected = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, card.Owner);
        if (selected == null) return;
        selected.SetToFreeThisTurn();
        CardCmd.PreviewCardPileAdd(await Bd.AddGeneratedCardToCombat(selected, PileType.Hand, card.Owner));
    }

    private static async Task PlayFtl(Ftl card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var playsFinished = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.HappenedThisTurn(card.CombatState) && entry.CardPlay.Card.Owner == card.Owner);
        if (playsFinished < card.DynamicVars["PlayMax"].IntValue)
            await CardPileCmd.Draw(choiceContext, 1m, card.Owner);
        else
            await Bd.ApplyPower<BdLockOnPower>(choiceContext, cardPlay.Target, card.IsUpgraded ? 2m : 1m, card.Owner.Creature, card);
    }

    private static async Task PlayNull(Null card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        var alreadyWeak = cardPlay.Target.HasPower<WeakPower>();
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await Bd.ApplyPower<WeakPower>(choiceContext, cardPlay.Target, card.DynamicVars.Weak.BaseValue, card.Owner.Creature, card);
        await OrbCmd.Channel<DarkOrb>(choiceContext, card.Owner);
        if (alreadyWeak)
            await OrbCmd.Channel<DarkOrb>(choiceContext, card.Owner);
    }

    private static bool IsCurrentTwoCost(CardModel card)
    {
        try
        {
            return !card.EnergyCost.CostsX
                   && card.CurrentStarCost <= 0
                   && !card.HasStarCostX
                   && card.EnergyCost.GetWithModifiers(CostModifiers.All) == 2;
        }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(AdaptiveStrike), "OnPlay")]
internal static class BdCustomRareAdaptiveStrikePlayPatch
{
    private static bool Prefix(
        AdaptiveStrike __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance)) return true;
        __result = BdCustomRareCardPlay.PlayAdaptiveStrike(__instance, choiceContext, cardPlay);
        return false;
    }
}

[HarmonyPatch(typeof(AllForOne), "OnPlay")]
internal static class BdCustomRareAllForOnePlayPatch
{
    private static bool Prefix(
        AllForOne __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance)) return true;
        __result = BdCustomRareCardPlay.PlayAllForOne(__instance, choiceContext, cardPlay);
        return false;
    }
}

[HarmonyPatch(typeof(BufferCard), "OnPlay")]
internal static class BdCustomRareBufferPlayPatch
{
    private static bool Prefix(
        BufferCard __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance)) return true;
        __result = BdCustomRareCardPlay.PlayBuffer(__instance, choiceContext, cardPlay);
        return false;
    }
}

[HarmonyPatch(typeof(FlakCannon), "OnPlay")]
internal static class BdCustomRareFlakCannonPlayPatch
{
    private static bool Prefix(
        FlakCannon __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance)) return true;
        __result = BdCustomRareCardPlay.PlayFlakCannon(__instance, choiceContext, cardPlay);
        return false;
    }
}

[HarmonyPatch(typeof(MeteorStrike), "OnPlay")]
internal static class BdCustomRareMeteorStrikePlayPatch
{
    private static bool Prefix(
        MeteorStrike __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance)) return true;
        __result = BdCustomRareCardPlay.PlayMeteorStrike(__instance, choiceContext, cardPlay);
        return false;
    }
}

[HarmonyPatch(typeof(MultiCast), "OnPlay")]
internal static class BdCustomRareMultiCastPlayPatch
{
    private static bool Prefix(
        MultiCast __instance,
        PlayerChoiceContext choiceContext,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance)) return true;
        __result = BdCustomRareCardPlay.PlayMultiCast(__instance, choiceContext);
        return false;
    }
}

[HarmonyPatch(typeof(Rainbow), "OnPlay")]
internal static class BdCustomRareRainbowPlayPatch
{
    private static bool Prefix(
        Rainbow __instance,
        PlayerChoiceContext choiceContext,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance)) return true;
        __result = BdCustomRareCardPlay.PlayRainbow(__instance, choiceContext);
        return false;
    }
}

internal static class BdCustomRareCardPlay
{
    internal static async Task PlayAdaptiveStrike(AdaptiveStrike card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        var clone = card.CreateClone();
        clone.EnergyCost.SetThisCombat(0);
        clone.AddKeyword(CardKeyword.Ethereal);
        CardCmd.PreviewCardPileAdd(await Bd.AddGeneratedCardToCombat(clone, PileType.Draw, card.Owner));
    }

    internal static async Task PlayAllForOne(AllForOne card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt", null, "blunt_attack.mp3")
            .WithHitVfxSpawnedAtBase()
            .Execute(choiceContext);

        var max = card.IsUpgraded ? 3 : 2;
        var candidates = PileType.Discard.GetPile(card.Owner).Cards
            .Where(IsCurrentZeroCostPlayable)
            .ToList();
        var selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            card.Owner,
            new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 0, max));
        await CardPileCmd.Add(selected, PileType.Hand);
    }

    internal static async Task PlayBuffer(BufferCard card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        await Bd.ApplyPower<BufferPower>(
            choiceContext,
            card.Owner.Creature,
            card.DynamicVars["BufferPower"].BaseValue,
            card.Owner.Creature,
            card);
        await CreatureCmd.GainBlock(card.Owner.Creature, 10m, ValueProp.Move, cardPlay);
    }

    internal static async Task PlayFlakCannon(FlakCannon card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        var statuses = card.Owner.PlayerCombatState.AllCards
            .Where(c => c.Type == CardType.Status && c.Pile.Type != PileType.Exhaust)
            .ToList();
        foreach (var status in statuses)
            await CardCmd.Exhaust(choiceContext, status);

        var hitCount = PileType.Exhaust.GetPile(card.Owner).Cards.Count;
        if (hitCount <= 0) return;
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).WithHitCount(hitCount).FromCard(card)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    internal static async Task PlayMeteorStrike(MeteorStrike card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
        await OrbCmd.Channel<PlasmaOrb>(choiceContext, card.Owner);
        await OrbCmd.Channel<PlasmaOrb>(choiceContext, card.Owner);
    }

    internal static async Task PlayMultiCast(MultiCast card, PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        var count = card.ResolveEnergyXValue() + (card.IsUpgraded ? 1 : 0);
        for (var i = 0; i < count; i++)
        {
            var next = card.Owner.PlayerCombatState.OrbQueue.Orbs.FirstOrDefault();
            if (next == null) break;
            var orbType = next.GetType();
            await OrbCmd.EvokeNext(choiceContext, card.Owner);
            await ChannelSameType(choiceContext, card.Owner, orbType);
            await Cmd.Wait(0.25f);
        }
    }

    private static async Task ChannelSameType(PlayerChoiceContext choiceContext, Player player, Type orbType)
    {
        if (orbType == typeof(LightningOrb)) await OrbCmd.Channel<LightningOrb>(choiceContext, player);
        else if (orbType == typeof(FrostOrb)) await OrbCmd.Channel<FrostOrb>(choiceContext, player);
        else if (orbType == typeof(DarkOrb)) await OrbCmd.Channel<DarkOrb>(choiceContext, player);
        else if (orbType == typeof(PlasmaOrb)) await OrbCmd.Channel<PlasmaOrb>(choiceContext, player);
        else if (orbType == typeof(GlassOrb)) await OrbCmd.Channel<GlassOrb>(choiceContext, player);
    }

    internal static async Task PlayRainbow(Rainbow card, PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);
        await OrbCmd.Channel<LightningOrb>(choiceContext, card.Owner);
        await OrbCmd.Channel<FrostOrb>(choiceContext, card.Owner);
        await OrbCmd.Channel<DarkOrb>(choiceContext, card.Owner);
        await OrbCmd.Channel<GlassOrb>(choiceContext, card.Owner);
        await OrbCmd.Channel<PlasmaOrb>(choiceContext, card.Owner);
    }

    private static bool IsCurrentZeroCostPlayable(CardModel card)
    {
        try
        {
            return !card.EnergyCost.CostsX
                   && !card.Keywords.Contains(CardKeyword.Unplayable)
                   && card.EnergyCost.GetWithModifiers(CostModifiers.All) == 0
                   && card.Type is CardType.Attack or CardType.Skill or CardType.Power;
        }
        catch { return false; }
    }
}

[HarmonyPatch]
internal static class BdCustomConsumingShadowPowerPatch
{
    private static bool Prepare() => AccessTools.DeclaredMethod(typeof(ConsumingShadowPower), "AfterSideTurnEnd") != null;
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(ConsumingShadowPower), "AfterSideTurnEnd");

    private static bool Prefix(
        ConsumingShadowPower __instance,
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> participants,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<ConsumingShadow>()) return true;
        __result = participants.Contains(__instance.Owner)
            ? Trigger(__instance, choiceContext)
            : Task.CompletedTask;
        return false;
    }

    internal static async Task Trigger(ConsumingShadowPower power, PlayerChoiceContext choiceContext)
    {
        for (var repeat = 0; repeat < power.Amount; repeat++)
        {
            foreach (var orb in power.Owner.Player.PlayerCombatState.OrbQueue.Orbs.OfType<DarkOrb>().ToList())
                await OrbCmd.Passive(choiceContext, orb, null);
        }
    }
}

[HarmonyPatch]
internal static class BdCustomConsumingShadowPowerV103Patch
{
    private static bool Prepare() => AccessTools.DeclaredMethod(typeof(ConsumingShadowPower), "AfterTurnEnd") != null;
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(ConsumingShadowPower), "AfterTurnEnd");

    private static bool Prefix(
        ConsumingShadowPower __instance,
        PlayerChoiceContext choiceContext,
        CombatSide side,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<ConsumingShadow>()) return true;
        __result = side == __instance.Owner.Side
            ? BdCustomConsumingShadowPowerPatch.Trigger(__instance, choiceContext)
            : Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class BdCustomCoolantStartSuppressionPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(CoolantPower), "AfterSideTurnStart");

    private static bool Prefix(ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<Coolant>()) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class BdCustomCoolantEndPatch
{
    private static bool Prepare() => AccessTools.DeclaredMethod(typeof(AbstractModel), "AfterSideTurnEnd") != null;
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(AbstractModel), "AfterSideTurnEnd");

    private static bool Prefix(AbstractModel __instance, object[] __args, ref Task __result)
    {
        if (__instance is not CoolantPower power || !BdCardVersionUpgrades.IsVersionEnabled<Coolant>())
            return true;
        var participants = __args.OfType<IEnumerable<Creature>>().FirstOrDefault();
        var context = __args.OfType<PlayerChoiceContext>().FirstOrDefault();
        if (participants == null || context == null || !participants.Contains(power.Owner))
        {
            __result = Task.CompletedTask;
            return false;
        }
        __result = Trigger(power);
        return false;
    }

    internal static Task Trigger(CoolantPower power)
    {
        var distinct = power.Owner.Player.PlayerCombatState.OrbQueue.Orbs
            .Select(orb => orb.Id)
            .Distinct()
            .Count();
        return distinct <= 0
            ? Task.CompletedTask
            : CreatureCmd.GainBlock(power.Owner, distinct * power.Amount, ValueProp.Unpowered, null);
    }
}

[HarmonyPatch]
internal static class BdCustomCoolantEndV103Patch
{
    private static bool Prepare() => AccessTools.DeclaredMethod(typeof(AbstractModel), "AfterTurnEnd") != null;
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(AbstractModel), "AfterTurnEnd");

    private static bool Prefix(AbstractModel __instance, object[] __args, ref Task __result)
    {
        if (__instance is not CoolantPower power || !BdCardVersionUpgrades.IsVersionEnabled<Coolant>())
            return true;
        var side = __args.OfType<CombatSide>().FirstOrDefault();
        if (side != power.Owner.Side)
        {
            __result = Task.CompletedTask;
            return false;
        }
        __result = BdCustomCoolantEndPatch.Trigger(power);
        return false;
    }
}

[HarmonyPatch]
internal static class BdCustomCreativeAiPowerPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(CreativeAiPower), "BeforeHandDraw");

    private static bool Prefix(CreativeAiPower __instance, object[] __args, ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<CreativeAi>()) return true;
        var player = __args.OfType<Player>().FirstOrDefault();
        var context = __args.OfType<PlayerChoiceContext>().FirstOrDefault();
        if (player == null || context == null || player != __instance.Owner.Player)
        {
            __result = Task.CompletedTask;
            return false;
        }
        __result = Trigger(__instance, player, context);
        return false;
    }

    private static async Task Trigger(CreativeAiPower power, Player player, PlayerChoiceContext choiceContext)
    {
        for (var i = 0; i < power.Amount; i++)
        {
            var choices = CardFactory.GetDistinctForCombat(
                    player,
                    player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                        .Where(card => card.Type == CardType.Power),
                    3,
                    player.RunState.Rng.CombatCardGeneration)
                .ToList();
            var selected = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, player);
            if (selected == null) continue;
            selected.SetToFreeThisTurn();
            CardCmd.PreviewCardPileAdd(await Bd.AddGeneratedCardToCombat(selected, PileType.Hand, player));
        }
    }
}

[HarmonyPatch]
internal static class BdCustomEchoFormPowerPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(EchoFormPower), nameof(EchoFormPower.ModifyCardPlayCount));

    private static bool Prefix(EchoFormPower __instance, CardModel card, int playCount, ref int __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<EchoForm>()) return true;
        __result = playCount;
        if (card.Owner.Creature != __instance.Owner) return false;
        var priorPlays = CombatManager.Instance.History.CardPlaysStarted.Count(entry =>
            entry.Actor == __instance.Owner &&
            entry.CardPlay.IsFirstInSeries &&
            entry.HappenedThisTurn(__instance.CombatState));
        if (priorPlays == 1)
            __result = playCount + __instance.Amount;
        return false;
    }
}

[HarmonyPatch]
internal static class BdCustomBeamCellHoverTipsPatch
{
    private static MethodBase? TargetMethod() => AccessTools.PropertyGetter(typeof(BeamCell), "ExtraHoverTips");

    private static bool Prefix(BeamCell __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance))
            return true;
        __result = [HoverTipFactory.FromPower<BdLockOnPower>()];
        return false;
    }
}

[HarmonyPatch]
internal static class BdCustomFightThroughHoverTipsPatch
{
    private static MethodBase? TargetMethod() => AccessTools.PropertyGetter(typeof(FightThrough), "ExtraHoverTips");

    private static bool Prefix(FightThrough __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled(__instance))
            return true;
        __result = [HoverTipFactory.FromCard<Dazed>()];
        return false;
    }
}

/// <summary>
/// Refract keeps its printed base cost at three, but its final combat cost is
/// two whenever its owner currently has at least one Glass orb.
/// </summary>
[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.TryModifyEnergyCostInCombat))]
internal static class BdCustomRefractCostPatch
{
    private static bool Prefix(AbstractModel __instance, CardModel card, decimal originalCost, ref decimal modifiedCost, ref bool __result)
    {
        if (__instance is not Refract source || card is not Refract)
            return true;
        if (!ReferenceEquals(source.Owner, card.Owner) || !BdCardVersionUpgrades.IsVersionEnabled(card))
            return true;
        if (!card.Owner.PlayerCombatState.OrbQueue.Orbs.Any(orb => orb is GlassOrb))
            return true;

        modifiedCost = Math.Min(originalCost, 2m);
        __result = modifiedCost != originalCost;
        return false;
    }
}

/// <summary>
/// The transformed Feral applies to the first zero-energy card of any type,
/// rather than only to zero-energy attacks. The native power still owns the
/// per-turn counter, stacking, flash, and reset behavior.
/// </summary>
[HarmonyPatch]
internal static class BdCustomFeralPowerResultPatch
{
    private static MethodBase? TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(FeralPower), nameof(FeralPower.ModifyCardPlayResultPileTypeAndPosition));

    private static bool Prefix(
        FeralPower __instance,
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position,
        ref ValueTuple<PileType, CardPilePosition> __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<Feral>())
            return true;

        __result = (pileType, position);
        if (card.Owner.Creature == __instance.Owner &&
            resources.EnergyValue <= 0 &&
            __instance.DisplayAmount > 0)
        {
            __result = (PileType.Hand, CardPilePosition.Top);
        }
        return false;
    }
}

[HarmonyPatch]
internal static class BdCustomHailstormPowerPatch
{
    private static bool Prepare() => AccessTools.DeclaredMethod(typeof(HailstormPower), "BeforeSideTurnEnd") != null;

    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(HailstormPower), "BeforeSideTurnEnd");

    private static bool Prefix(
        HailstormPower __instance,
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<Hailstorm>())
            return true;
        __result = Trigger(__instance, choiceContext, participants);
        return false;
    }

    private static async Task Trigger(HailstormPower power, PlayerChoiceContext choiceContext, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(power.Owner)) return;
        var frostCount = power.Owner.Player.PlayerCombatState.OrbQueue.Orbs.Count(orb => orb is FrostOrb);
        if (frostCount <= 0) return;
        await CreatureCmd.Damage(
            choiceContext,
            power.CombatState.HittableEnemies,
            power.Amount * frostCount,
            MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,
            power.Owner);
    }
}

[HarmonyPatch]
internal static class BdCustomHailstormPowerV103Patch
{
    private static bool Prepare() => AccessTools.DeclaredMethod(typeof(HailstormPower), "BeforeTurnEnd") != null;

    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(HailstormPower), "BeforeTurnEnd");

    private static bool Prefix(
        HailstormPower __instance,
        PlayerChoiceContext choiceContext,
        CombatSide side,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<Hailstorm>())
            return true;
        __result = side == __instance.Owner.Side
            ? Trigger(__instance, choiceContext)
            : Task.CompletedTask;
        return false;
    }

    private static async Task Trigger(HailstormPower power, PlayerChoiceContext choiceContext)
    {
        var frostCount = power.Owner.Player.PlayerCombatState.OrbQueue.Orbs.Count(orb => orb is FrostOrb);
        if (frostCount <= 0) return;
        await CreatureCmd.Damage(
            choiceContext,
            power.CombatState.HittableEnemies,
            power.Amount * frostCount,
            MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,
            power.Owner);
    }
}

[HarmonyPatch]
internal static class BdCustomIterationPowerPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(IterationPower), "AfterCardDrawn");

    private static void Postfix(
        IterationPower __instance,
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw,
        ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<Iteration>() || card.Owner.Creature != __instance.Owner || card.Type != CardType.Status)
            return;

        var firstStatus = CombatManager.Instance.History.Entries.OfType<CardDrawnEntry>().Count(entry =>
            entry.HappenedThisTurn(__instance.CombatState)
            && entry.Actor == __instance.Owner
            && entry.Card.Type == CardType.Status) <= 1;
        if (firstStatus)
            __result = FinishAndExhaust(__result, choiceContext, card);
    }

    private static async Task FinishAndExhaust(Task original, PlayerChoiceContext choiceContext, CardModel card)
    {
        await original;
        try
        {
            if (card.Pile?.Type == PileType.Hand)
                await CardCmd.Exhaust(choiceContext, card);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] Iteration could not exhaust drawn status: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

[HarmonyPatch]
internal static class BdCustomLoopPowerPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(LoopPower), "AfterPlayerTurnStart");

    private static bool Prefix(LoopPower __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<Loop>())
            return true;
        __result = Trigger(__instance, choiceContext, player);
        return false;
    }

    private static async Task Trigger(LoopPower power, PlayerChoiceContext choiceContext, Player player)
    {
        if (player != power.Owner.Player) return;
        var orbs = player.PlayerCombatState.OrbQueue.Orbs.ToList();
        if (orbs.Count == 0) return;
        await OrbCmd.Passive(choiceContext, orbs[0], null);
        await Cmd.Wait(0.25f);
        if (orbs.Count > 1 && player.PlayerCombatState.OrbQueue.Orbs.Contains(orbs[^1]))
        {
            await OrbCmd.Passive(choiceContext, orbs[^1], null);
            await Cmd.Wait(0.25f);
        }
    }
}

[HarmonyPatch]
internal static class BdCustomSmokestackPowerPatch
{
    private sealed class TriggerState
    {
        public int Round = int.MinValue;
        public bool Drew;
    }

    private static readonly ConditionalWeakTable<SmokestackPower, TriggerState> States = new();

    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(SmokestackPower), "AfterCardGeneratedForCombat");

    private static bool Prefix(SmokestackPower __instance, object[] __args, ref Task __result)
    {
        if (!BdCardVersionUpgrades.IsVersionEnabled<Smokestack>())
            return true;

        if (__args.Length < 2 || __args[0] is not CardModel card)
            return true;

        // v0.103 uses (CardModel card, bool addedByPlayer), while newer PC
        // builds use (CardModel card, Player? creator). Avoid binding the
        // Harmony patch to either version's second argument name/type.
        var generatedByOwner = __args[1] switch
        {
            bool addedByPlayer => addedByPlayer && card.Owner?.Creature == __instance.Owner,
            Player creator => creator.Creature == __instance.Owner,
            _ => false
        };

        __result = Trigger(__instance, card, generatedByOwner);
        return false;
    }

    private static async Task Trigger(SmokestackPower power, CardModel card, bool generatedByOwner)
    {
        if (card.Type != CardType.Status || !generatedByOwner)
            return;

        var context = new ThrowingPlayerChoiceContext();
        await CreatureCmd.Damage(
            context,
            power.CombatState.HittableEnemies,
            power.Amount,
            MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,
            power.Owner,
            null);

        var state = States.GetOrCreateValue(power);
        var round = power.CombatState.RoundNumber;
        if (state.Round != round)
        {
            state.Round = round;
            state.Drew = false;
        }
        if (!state.Drew)
        {
            state.Drew = true;
            await CardPileCmd.Draw(context, 1m, power.Owner.Player);
        }
    }
}

[HarmonyPatch]
internal static class BdCustomSubroutinePowerPatch
{
    private sealed class TriggerState
    {
        public int Round = int.MinValue;
        public bool Drew;
    }

    private static readonly ConditionalWeakTable<SubroutinePower, TriggerState> States = new();
    private static readonly FieldInfo? InternalDataField = AccessTools.Field(typeof(PowerModel), "_internalData");

    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(SubroutinePower), "AfterCardPlayed");

    private static void Prefix(SubroutinePower __instance, object[] __args, out bool __state)
    {
        var cardPlay = __args.Length > 1 ? __args[1] as CardPlay : null;
        __state = BdCardVersionUpgrades.IsVersionEnabled<Subroutine>()
            && cardPlay != null
            && WasTrackedByVanilla(__instance, cardPlay.Card);
    }

    private static void Postfix(
        SubroutinePower __instance,
        object[] __args,
        bool __state,
        ref Task __result)
    {
        if (!__state) return;
        if (__args.Length == 0 || __args[0] is not PlayerChoiceContext choiceContext)
            return;
        var state = States.GetOrCreateValue(__instance);
        var round = __instance.CombatState.RoundNumber;
        if (state.Round != round)
        {
            state.Round = round;
            state.Drew = false;
        }
        if (state.Drew) return;
        state.Drew = true;
        __result = FinishAndDraw(__result, choiceContext, __instance.Owner.Player);
    }

    private static bool WasTrackedByVanilla(SubroutinePower power, CardModel card)
    {
        try
        {
            var data = InternalDataField?.GetValue(power);
            var field = data == null ? null : AccessTools.Field(data.GetType(), "amountsForPlayedCards");
            return field?.GetValue(data) is IDictionary dictionary && dictionary.Contains(card);
        }
        catch { return false; }
    }

    private static async Task FinishAndDraw(Task original, PlayerChoiceContext choiceContext, Player player)
    {
        await original;
        await CardPileCmd.Draw(choiceContext, 1m, player);
    }
}

[HarmonyPatch]
internal static class BdCardVersionRocketPunchGenerationPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(RocketPunch), "AfterCardGeneratedForCombat");

    private static bool Prefix(RocketPunch __instance, object[] __args, ref Task __result)
    {
        __result = Task.CompletedTask;
        try
        {
            if (__args.Length == 0 || __args[0] is not CardModel generated || generated.Type != CardType.Status)
                return false;
            if (generated.Owner != __instance.Owner)
                return false;

            var createdByOwner = __args.Length < 2;
            if (__args.Length >= 2 && __args[1] is bool addedByPlayer)
                createdByOwner = addedByPlayer;
            else if (__args.Length >= 2 && __args[1] is Player creator)
                createdByOwner = ReferenceEquals(creator, __instance.Owner);

            if (!createdByOwner)
                return false;

            if (BdCardVersionUpgrades.IsVersionEnabled(__instance))
                __instance.EnergyCost.SetUntilPlayed(0);
            else
                __instance.EnergyCost.SetThisTurnOrUntilPlayed(0);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] Rocket Punch historical behavior skipped: {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }
}

[HarmonyPatch]
internal static class BdCardVersionShatterPlayPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(Shatter), "OnPlay");

    private static bool Prefix(Shatter __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
    {
        __result = Play(__instance, choiceContext);
        return false;
    }

    private static async Task Play(Shatter card, PlayerChoiceContext choiceContext)
    {
        var attack = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card);
        if (Cards.Bd.TryTargetAllOpponents(attack, card))
        {
            await attack.WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        }
        else
        {
            // Defensive fallback for an unknown future AttackCommand signature.
            // Use one stable single-target command per living opponent rather
            // than leaving the card action faulted and stuck in the play area.
            foreach (var target in Cards.Bd.Enemies(card).ToList())
            {
                await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
            }
        }
        var orbCount = card.Owner.PlayerCombatState.OrbQueue.Orbs.Count;
        for (var i = 0; i < orbCount; i++)
        {
            await OrbCmd.EvokeNext(choiceContext, card.Owner, dequeue: false);
            await OrbCmd.EvokeNext(choiceContext, card.Owner);
        }
    }
}

[HarmonyPatch]
internal static class BdCardVersionTeslaCoilPlayPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(TeslaCoil), "OnPlay");

    private static bool Prefix(TeslaCoil __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
    {
        __result = Play(__instance, choiceContext, cardPlay);
        return false;
    }

    private static async Task Play(TeslaCoil card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        var lightningOrbs = card.Owner.PlayerCombatState.OrbQueue.Orbs.OfType<LightningOrb>().ToList();
        foreach (var orb in lightningOrbs)
        {
            await OrbCmd.Passive(choiceContext, orb, cardPlay.Target);
            if (card.IsUpgraded && BdCardVersionUpgrades.IsVersionEnabled(card))
                await OrbCmd.Passive(choiceContext, orb, cardPlay.Target);
        }
    }
}

[HarmonyPatch]
internal static class BdCardVersionFuelPlayPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(Fuel), "OnPlay");

    private static bool Prefix(Fuel __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
    {
        __result = Play(__instance, choiceContext);
        return false;
    }

    private static async Task Play(Fuel card, PlayerChoiceContext choiceContext)
    {
        await PlayerCmd.GainEnergy(card.DynamicVars.Energy.BaseValue, card.Owner);
        var draw = card.DynamicVars["Cards"].IntValue;
        if (draw > 0)
            await CardPileCmd.Draw(choiceContext, draw, card.Owner);
    }
}

[HarmonyPatch]
internal static class BdCardVersionScrapePlayPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(Scrape), "OnPlay");

    private static bool Prefix(Scrape __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
    {
        __result = Play(__instance, choiceContext, cardPlay);
        return false;
    }

    private static async Task Play(Scrape card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var useV108 = BdCardVersionUpgrades.IsVersionEnabled(card);
        var drawn = await CardPileCmd.Draw(choiceContext, card.DynamicVars.Cards.IntValue, card.Owner);
        var toDiscard = drawn.Where(c =>
        {
            var modifiers = useV108 ? CostModifiers.All : CostModifiers.Local;
            return c.EnergyCost.GetWithModifiers(modifiers) != 0 || c.EnergyCost.CostsX || c.CurrentStarCost > 0 || c.HasStarCostX;
        });
        await CardCmd.Discard(choiceContext, toDiscard);
    }
}
