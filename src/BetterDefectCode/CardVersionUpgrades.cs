using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;

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
        typeof(Sunder), typeof(TrashToTreasure)
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
            ["CARD.TESLA_COIL"] = ("v0.105", "普通升级由6伤/闪电触发1次改为4伤/闪电触发2次"),
            ["CARD.UPROAR"] = ("v0.105", "伤害由5(7)提高到6(8)"),
            ["CARD.FUSION"] = ("v0.106", "耗能由2(1)改为1；基础牌消耗，普通升级移除消耗"),
            ["CARD.SYNTHESIS"] = ("v0.106", "伤害由12(18)提高到14(20)"),
            ["CARD.COMPACT"] = ("v0.99", "生成的燃料由获得1(2)能量改为获得1能量并抽1(2)张牌"),
            ["CARD.MOMENTUM_STRIKE"] = ("v0.108", "伤害由10(13)提高到11(15)"),
            ["CARD.SCRAPE"] = ("v0.108", "正确保留被全局临时效果降为0费的牌"),
            ["CARD.SUNDER"] = ("v0.109", "伤害由24(32)提高到26(34)"),
            ["CARD.TRASH_TO_TREASURE"] = ("v0.99", "普通升级由耗能降为0改为获得固有")
        };
    private static readonly FieldInfo? EnergyBaseField = AccessTools.Field(typeof(CardEnergyCost), "_base");
    private static readonly FieldInfo? KeywordsField = AccessTools.Field(typeof(CardModel), "_keywords");

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
        Uproar => "v0.105",
        Fusion => "v0.106",
        Synthesis => "v0.106",
        Compact => "v0.99",
        MomentumStrike => "v0.108",
        Scrape => "v0.108",
        Sunder => "v0.109",
        TrashToTreasure => "v0.99",
        _ => VersionedCardSpecs.TryGetValue(SafeCardId(card), out var spec) ? spec.Version : "历史版本"
    };

    internal static string GetTargetEffectSummary(CardModel card) => card switch
    {
        Hotfix => "基础牌不再消耗；普通升级额外把集中从2提高到3",
        RocketPunch => "生成状态牌后，0费持续到这张牌被打出，而不是只持续到回合结束",
        Voltaic => "耗能从3降为2；普通升级仍会移除消耗",
        Hyperbeam => "伤害由26(34)提高到30(38)",
        Shatter => "伤害由7(11)提高到11(15)，移除消耗；所有充能球仍激发两次",
        TeslaCoil => "普通升级由6伤/闪电触发1次改为4伤/闪电触发2次",
        Uproar => "伤害由5(7)提高到6(8)",
        Fusion => "耗能由2(1)改为1；基础牌消耗，普通升级移除消耗",
        Synthesis => "伤害由12(18)提高到14(20)",
        Compact => "生成的燃料由获得1(2)能量改为获得1能量并抽1(2)张牌",
        MomentumStrike => "伤害由10(13)提高到11(15)",
        Scrape => "正确保留被全局临时效果降为0费的牌",
        Sunder => "伤害由24(32)提高到26(34)",
        TrashToTreasure => "普通升级由耗能降为0改为获得固有",
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
                SetDynamic(card, "Damage", upgradedVersion
                    ? plus ? 8m : 6m
                    : plus ? 7m : 5m);
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
                UpgradeDynamicTo(card, "Damage", upgradedVersion ? 8m : 7m);
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
        }


        if (!VersionedCardTypeSet.Contains(card.GetType()) && card is not Fuel)
            ApplyNormalUpgradeById(card, upgradedVersion);
    }

    private static void ApplyVersionById(CardModel card, bool upgradedVersion, bool plus)
    {
        switch (SafeCardId(card).ToUpperInvariant())
        {
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
                SetDynamic(card, "Damage", upgradedVersion ? plus ? 8m : 6m : plus ? 7m : 5m);
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
        }
    }

    private static void ApplyNormalUpgradeById(CardModel card, bool upgradedVersion)
    {
        switch (SafeCardId(card).ToUpperInvariant())
        {
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
            case "CARD.UPROAR": UpgradeDynamicTo(card, "Damage", upgradedVersion ? 8m : 7m); break;
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
        }
    }

    internal static bool IsVersionEnabled(CardModel card) => IsEligible(card) && BdDynamicOdds.IsCardVersionUpgraded(card);

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

[HarmonyPatch]
internal static class BdCardVersionNormalUpgradePatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        BdCardVersionUpgrades.UpgradeMethodTypes
            .Select(t => AccessTools.DeclaredMethod(t, "OnUpgrade"))
            .Where(m => m != null)!;

    private static bool Prefix(CardModel __instance)
    {
        BdCardVersionUpgrades.ApplyNormalUpgrade(__instance);
        return false;
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
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card)
            .TargetingAllOpponents(card.CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
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
