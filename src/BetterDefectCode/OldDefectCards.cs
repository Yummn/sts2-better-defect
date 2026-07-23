using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using BetterDefect.Cards;

namespace BetterDefect;

internal static class OldDefectCards
{
    private static readonly Type[] CardTypes =
    {
        // Hidden in v103 but omitted from the visible Defect pool.
        typeof(HelloWorld), typeof(Rebound), typeof(RipAndTear), typeof(Stack),

        // Recreated StS1 Defect cards. Equilibrium remains intentionally excluded.
        typeof(BdRecursion), typeof(BdSteamBarrier), typeof(BdStreamline), typeof(BdAggregate),
        typeof(BdAutoShields), typeof(BdBlizzard), typeof(BdBullseye), typeof(BdConsume),
        typeof(BdDoomAndGloom), typeof(BdForceField), typeof(BdHeatsinks), typeof(BdMelter),
        typeof(BdRecycle), typeof(BdReinforcedBody), typeof(BdReprogram), typeof(BdSelfRepair),
        typeof(BdStaticDischarge), typeof(BdSeek), typeof(BdCoreSurge), typeof(BdElectrodynamics),
        typeof(BdFission), typeof(BdThunderStrike),
    };

    private static readonly Dictionary<Type, CardRarity> Rarities = new()
    {
        [typeof(HelloWorld)] = CardRarity.Uncommon,
        [typeof(Rebound)] = CardRarity.Common,
        [typeof(RipAndTear)] = CardRarity.Uncommon,
        [typeof(Stack)] = CardRarity.Common,
    };

    private static readonly HashSet<Type> CardTypeSet = new(CardTypes);
    private static readonly Dictionary<Type, bool> RestoredTypeCache = new();
    private static CardModel[]? _cachedCards;
    private static bool _loggedAppendTo;

    public static IEnumerable<Type> Types => CardTypes;
    public static IEnumerable<CardModel> Cards => GetCards();

    public static void EnsureInjected()
    {
        _cachedCards = null;
        RestoredTypeCache.Clear();
        var ok = 0;
        foreach (var type in CardTypes)
        {
            try { ModelDb.Inject(type); ok++; }
            catch (Exception ex) { MainFile.Logger.Warn($"[BetterDefect] failed to inject {type.Name}: {ex.Message}"); }
        }
        foreach (var type in OldDefectPowers.Types)
        {
            try { ModelDb.Inject(type); }
            catch (Exception ex) { MainFile.Logger.Warn($"[BetterDefect] failed to inject power {type.Name}: {ex.Message}"); }
        }
        MainFile.Logger.Info($"[BetterDefect] checked old Defect card model injection: attempted={CardTypes.Length}, injected={ok}.");
        ResetCardPoolCaches();
    }

    public static IEnumerable<CardModel> AppendTo(IEnumerable<CardModel> cards)
    {
        var list = cards.ToList();
        var seen = list.Select(SafeCardId).ToHashSet(StringComparer.Ordinal);
        var added = 0;
        foreach (var card in GetCards())
        {
            if (seen.Add(SafeCardId(card))) { list.Add(card); added++; }
        }
        if (!_loggedAppendTo)
        {
            _loggedAppendTo = true;
            MainFile.Logger.Info($"[BetterDefect] restored {added} old Defect cards to the Defect card pool.");
        }
        return list;
    }

    public static bool IsRestored(CardModel card)
    {
        var type = card.GetType();
        if (RestoredTypeCache.TryGetValue(type, out var cached))
            return cached;

        var restored = CardTypeSet.Contains(type) || CardTypes.Any(t => t.IsAssignableFrom(type));
        RestoredTypeCache[type] = restored;
        return restored;
    }

    public static bool TryGetRarity(CardModel card, out CardRarity rarity)
    {
        foreach (var kv in Rarities)
        {
            if (kv.Key.IsInstanceOfType(card)) { rarity = kv.Value; return true; }
        }
        rarity = default;
        return false;
    }

    public static CardPoolModel? GetDefectPool()
    {
        try { return ModelDb.CardPool<DefectCardPool>(); }
        catch { return ModelDb.AllCharacterCardPools.FirstOrDefault(p => SafeModelId(p) == "CARD_POOL.DEFECT_CARD_POOL"); }
    }
    private static IEnumerable<CardModel> GetCards()
    {
        if (_cachedCards != null)
            return _cachedCards;

        var cards = new List<CardModel>(CardTypes.Length);
        foreach (var type in CardTypes)
        {
            var card = FindCard(type);
            if (card != null) cards.Add(card);
        }
        _cachedCards = cards.ToArray();
        return _cachedCards;
    }

    private static CardModel? FindCard(Type type)
    {
        try { ModelDb.Inject(type); } catch { }
        try { return AccessTools.Method(typeof(ModelDb), "Get", new[] { typeof(Type) })?.Invoke(null, new object[] { type }) as CardModel; } catch { }
        try
        {
            var method = AccessTools.Method(typeof(ModelDb), nameof(ModelDb.Card));
            return method?.MakeGenericMethod(type).Invoke(null, null) as CardModel;
        }
        catch { }
        try { return ModelDb.AllCards.FirstOrDefault(type.IsInstanceOfType); } catch { }
        return null;
    }

    public static CardModel[] AppendToArray(IEnumerable<CardModel> cards)
    {
        return AppendTo(cards).ToArray();
    }

    public static void ResetCardPoolCaches()
    {
        try
        {
            var pool = GetDefectPool();
            if (pool != null)
            {
                AccessTools.Field(typeof(CardPoolModel), "_allCards")?.SetValue(pool, null);
                AccessTools.Field(typeof(CardPoolModel), "_allCardIds")?.SetValue(pool, null);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to reset Defect card pool cache: {ex.Message}");
        }

        try { AccessTools.Field(typeof(ModelDb), "_allCards")?.SetValue(null, null); } catch { }
    }

    private static string SafeCardId(CardModel card) => SafeModelId(card);
    private static string SafeModelId(AbstractModel model)
    {
        try { return model.Id.ToString(); }
        catch { return model.GetType().FullName ?? model.GetType().Name; }
    }
}

internal static class OldDefectPowers
{
    public static readonly Type[] Types =
    {
        typeof(BdHeatsinksPower), typeof(BdSelfRepairPower), typeof(BdStaticDischargePower),
        typeof(BdElectrodynamicsPower), typeof(BdLockOnPower),
    };
}


