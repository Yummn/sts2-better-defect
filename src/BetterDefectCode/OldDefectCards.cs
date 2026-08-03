using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using BetterDefect.Cards;

namespace BetterDefect;

internal static class OldDefectCards
{
    private static readonly Type[] RestoredCardTypes =
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

    private static readonly Type[] AddedCardTypes =
    {
        // Defect-specific Ancient card selected by Darv's Dusty Tome.
        typeof(BdReworkedBiasedCognition),
    };

    private static readonly Type[] CardTypes = RestoredCardTypes.Concat(AddedCardTypes).ToArray();

    private static readonly Dictionary<Type, CardRarity> Rarities = new()
    {
        [typeof(HelloWorld)] = CardRarity.Uncommon,
        [typeof(Rebound)] = CardRarity.Common,
        [typeof(RipAndTear)] = CardRarity.Uncommon,
        [typeof(Stack)] = CardRarity.Common,
    };

    private static readonly HashSet<Type> RestoredCardTypeSet = new(RestoredCardTypes);
    private static readonly HashSet<Type> ManagedCardTypeSet = new(CardTypes);
    private static readonly Dictionary<Type, bool> RestoredTypeCache = new();
    private static readonly FieldInfo? CardRarityBackingField =
        AccessTools.Field(typeof(CardModel), "<Rarity>k__BackingField");
    private static CardModel[]? _cachedCards;
    private static bool _loggedAppendTo;
    private static bool _loggedLibraryOrdering;
    private static bool _loggedRarityNormalization;

    private sealed class CardLibraryInitialComparer : IComparer<CardModel>
    {
        private readonly List<CardPoolModel> _cardPools;

        public CardLibraryInitialComparer(List<CardPoolModel> cardPools)
        {
            _cardPools = cardPools;
        }

        public int Compare(CardModel? x, CardModel? y)
        {
            if (x == null) return y == null ? 0 : -1;
            if (y == null) return 1;

            var poolOrder = _cardPools.IndexOf(x.Pool).CompareTo(_cardPools.IndexOf(y.Pool));
            if (poolOrder != 0) return poolOrder;

            var rarityOrder = x.Rarity.CompareTo(y.Rarity);
            if (rarityOrder != 0) return rarityOrder;

            try { return x.Id.CompareTo(y.Id); }
            catch { return StringComparer.Ordinal.Compare(SafeCardId(x), SafeCardId(y)); }
        }
    }

    public static IEnumerable<Type> Types => CardTypes;
    public static IEnumerable<CardModel> Cards => GetCards();

    public static void EnsureInjected(bool resetGlobalCards = true)
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
        MainFile.Logger.Info($"[BetterDefect] checked managed Defect card model injection: attempted={CardTypes.Length}, injected={ok}.");
        ResetCardPoolCaches(resetGlobalCards);
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
            MainFile.Logger.Info($"[BetterDefect] appended {added} managed Defect cards to the Defect card pool.");
        }
        return list;
    }

    public static bool IsRestored(CardModel card)
    {
        var type = card.GetType();
        if (RestoredTypeCache.TryGetValue(type, out var cached))
            return cached;

        var restored = RestoredCardTypeSet.Contains(type) || RestoredCardTypes.Any(t => t.IsAssignableFrom(type));
        RestoredTypeCache[type] = restored;
        return restored;
    }

    public static bool IsManaged(CardModel card)
    {
        var type = card.GetType();
        return ManagedCardTypeSet.Contains(type) || CardTypes.Any(t => t.IsAssignableFrom(type));
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
            if (card != null)
            {
                NormalizeRestoredRarity(card);
                cards.Add(card);
            }
        }
        _cachedCards = cards.ToArray();
        return _cachedCards;
    }

    /// <summary>
    /// v103 still ships Hello World, Rebound, Rip and Tear, and Stack, but
    /// marks all four as Event cards. Their green Event banner therefore
    /// survives on Android because native virtual-property detours are skipped
    /// there for startup safety. Write the intended StS1 rarity into the
    /// auto-property backing field so every consumer (banner material, filters,
    /// sorting and reward rarity) sees the same corrected value without
    /// depending on Harmony.
    /// </summary>
    private static bool NormalizeRestoredRarity(CardModel card)
    {
        if (!TryGetRarity(card, out var rarity))
            return false;

        try
        {
            if (CardRarityBackingField == null)
            {
                if (!_loggedRarityNormalization)
                {
                    _loggedRarityNormalization = true;
                    MainFile.Logger.Warn("[BetterDefect] CardModel rarity backing field was not found; restored v103 cards may retain Event visuals.");
                }
                return false;
            }

            CardRarityBackingField.SetValue(card, rarity);
            return card.Rarity == rarity;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to normalize restored rarity for {card.GetType().Name}: {ex.Message}");
            return false;
        }
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

    public static void ResetCardPoolCaches(bool resetGlobalCards = true)
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

        if (resetGlobalCards)
        {
            try { AccessTools.Field(typeof(ModelDb), "_allCards")?.SetValue(null, null); } catch { }
        }
    }

    /// <summary>
    /// Android v103 installs Harmony patches after ModelDb.Preload has already
    /// materialized the vanilla 88-card Defect pool.  Re-inject the restored
    /// models, invalidate both cached enumerations, and eagerly rebuild the
    /// pool after the GenerateAllCards postfix is present.  Eager rebuilding
    /// also prevents a very fast encyclopedia open from retaining an old
    /// 88-card NCardLibraryGrid for the rest of that screen instance.
    /// </summary>
    public static void RefreshAfterDeferredPatchInstall()
    {
        try
        {
            // Keep the concrete global snapshot produced by ModelDb.Preload.
            // Replacing it with a fresh lazy query this late can leave the
            // encyclopedia with cards whose CardModel.Pool was never resolved.
            var existingGlobal = ModelDb.AllCards.ToArray();
            EnsureInjected(resetGlobalCards: false);

            var pool = GetDefectPool();
            if (pool == null)
            {
                MainFile.Logger.Error("[BetterDefect] deferred old-card refresh could not resolve the Defect card pool.");
                return;
            }

            var rebuilt = pool.AllCards.ToArray();
            var managed = rebuilt.Count(IsManaged);
            var restored = rebuilt.Count(IsRestored);

            // Keep a detour-independent fallback.  If the Android Harmony
            // backend accepted the patch class but failed to route this first
            // call through its postfix, write the complete array directly to
            // CardPoolModel's cache instead of silently exposing only 88 cards.
            if (managed < CardTypes.Length)
            {
                rebuilt = AppendToArray(rebuilt);
                AccessTools.Field(typeof(CardPoolModel), "_allCards")?.SetValue(pool, rebuilt);
                AccessTools.Field(typeof(CardPoolModel), "_allCardIds")?.SetValue(pool, null);
                managed = rebuilt.Count(IsManaged);
                restored = rebuilt.Count(IsRestored);
                MainFile.Logger.Warn($"[BetterDefect] deferred pool rebuild used direct cache fallback; total={rebuilt.Length}, managed={managed}, restored={restored}.");
            }

            // The encyclopedia's Defect filter is literally
            // `card.Pool is DefectCardPool`. Bind all rebuilt canonical cards
            // explicitly instead of depending on a late lazy AllCardIds lookup.
            var cardPoolField = AccessTools.Field(typeof(CardModel), "_pool");
            var normalizedRarities = 0;
            foreach (var card in rebuilt)
            {
                cardPoolField?.SetValue(card, pool);
                if (NormalizeRestoredRarity(card))
                    normalizedRarities++;
            }

            AccessTools.Field(typeof(CardPoolModel), "_allCards")?.SetValue(pool, rebuilt);
            AccessTools.Field(typeof(CardPoolModel), "_allCardIds")?.SetValue(pool, null);

            var merged = new List<CardModel>(existingGlobal.Length + rebuilt.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in existingGlobal)
            {
                if (!IsDefectPoolCard(card, pool) && seen.Add(SafeCardId(card)))
                    merged.Add(card);
            }
            foreach (var card in rebuilt)
            {
                if (seen.Add(SafeCardId(card)))
                    merged.Add(card);
            }

            var globalCards = merged.ToArray();
            AccessTools.Field(typeof(ModelDb), "_allCards")?.SetValue(null, globalCards);
            var globalRestored = globalCards.Count(IsRestored);
            var globalDefect = globalCards.Count(card => IsDefectPoolCard(card, pool));
            MainFile.Logger.Info(
                $"[BetterDefect] deferred old-card refresh complete: pool={rebuilt.Length}, " +
                $"poolManaged={managed}/{CardTypes.Length}, poolRestored={restored}/{RestoredCardTypes.Length}, " +
                $"globalRestored={globalRestored}/{RestoredCardTypes.Length}, " +
                $"globalDefect={globalDefect}, normalizedV103Rarities={normalizedRarities}/{Rarities.Count}.");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[BetterDefect] deferred old-card refresh failed: {ex}");
        }
    }

    /// <summary>
    /// The main-menu card library scene can run NCardLibraryGrid._Ready before
    /// Android finishes its delayed patch queue. Its _allCards list then keeps
    /// the pre-repair CardModel instances, so the Defect filter returns zero
    /// even though ModelDb now contains the complete 114-card canonical set.
    /// Replace that stale snapshot once the real encyclopedia grid is visible.
    /// </summary>
    public static bool RefreshCardLibraryGridIfStale(NCardLibraryGrid grid)
    {
        try
        {
            var allCardsField = AccessTools.Field(typeof(NCardLibraryGrid), "_allCards");
            if (allCardsField?.GetValue(grid) is not List<CardModel> gridCards)
                return false;

            var canonical = ModelDb.AllCards
                .Where(card => card.ShouldShowInCardLibrary)
                .ToList();
            canonical.Sort(new CardLibraryInitialComparer(ModelDb.AllCardPools.ToList()));
            var canonicalDefect = canonical.Count(IsDefectPoolCard);
            var gridDefect = gridCards.Count(IsDefectPoolCard);
            var gridManaged = gridCards.Count(IsManaged);
            var gridRestored = gridCards.Count(IsRestored);
            var orderMatches = gridCards.Count == canonical.Count &&
                gridCards.Select(SafeCardId).SequenceEqual(canonical.Select(SafeCardId), StringComparer.Ordinal);
            if (gridCards.Count == canonical.Count &&
                gridDefect == canonicalDefect &&
                gridManaged == CardTypes.Length &&
                gridRestored == RestoredCardTypes.Length &&
                orderMatches)
            {
                if (!_loggedLibraryOrdering)
                {
                    _loggedLibraryOrdering = true;
                    MainFile.Logger.Info(
                        $"[BetterDefect] encyclopedia card ordering verified: " +
                        $"total={gridCards.Count}, defect={gridDefect}, managed={gridManaged}, restored={gridRestored}, " +
                        "order=pool/rarity/card-id.");
                }
                return false;
            }

            gridCards.Clear();
            gridCards.AddRange(canonical);
            grid.RefreshVisibility();
            _loggedLibraryOrdering = true;
            MainFile.Logger.Info(
                $"[BetterDefect] refreshed stale encyclopedia card snapshot: " +
                $"total={gridCards.Count}, defect={canonicalDefect}, managed={CardTypes.Length}, restored={RestoredCardTypes.Length}, " +
                $"rarityOrderRepaired={!orderMatches}.");
            return true;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] stale encyclopedia snapshot refresh failed: {ex.Message}");
            return false;
        }
    }

    private static bool IsDefectPoolCard(CardModel card, CardPoolModel pool)
    {
        try { return ReferenceEquals(card.Pool, pool) || card.Pool is DefectCardPool; }
        catch { return false; }
    }

    private static bool IsDefectPoolCard(CardModel card)
    {
        try { return card.Pool is DefectCardPool; }
        catch { return false; }
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
        typeof(BdElectrodynamicsPower), typeof(BdLockOnPower), typeof(BdBulkUpPower),
        typeof(BdScrapeTemporaryStrengthPower), typeof(BdBullseyeTargetPower),
        typeof(BdSpinnerNoDecayPower), typeof(BdReworkedBiasedCognitionPower),
    };
}


