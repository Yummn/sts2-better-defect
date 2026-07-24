using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace BetterDefect;

/// <summary>
/// Cross-run dynamic reward weighting for the expanded BetterDefect card pool.
/// Vanilla still rolls card rarity first; this only changes the choice inside that rarity.
/// Choices made in the current run update the persisted table, but the active reward snapshot is frozen until the next run.
/// </summary>
internal static class BdDynamicOdds
{
    public const int NormalPointLimit = 25;
    public const int OverclockPointLimit = 35;
    public const int MaxCardPointBudget = 50;
    private const string ConfigFileName = "BetterDefect.DynamicOdds.cfg";
    // Do not use a top-level *.json file for learned weights: on Android the
    // built-in mod scanner treats every json file under a mod folder as a
    // possible manifest before our initializer runs.
    private const string WeightsFileName = "BetterDefect.DynamicOdds.weights.dat";
    private const string LegacyWeightsFileName = "BetterDefect.DynamicOdds.weights.json";
    private const string RuntimeFolderName = "BetterDefect";

    private static BdDynamicOddsConfig? _config;
    private static BdDynamicOddsWeights? _persistedWeights;
    private static Dictionary<string, Dictionary<string, float>> _activeWeightsByRarity = new(StringComparer.Ordinal);
    private static HashSet<string> _activeDisabledCards = new(StringComparer.Ordinal);
    private static object? _activeRunState;
    private static readonly object StateLock = new();
    private static readonly object TypeCacheLock = new();
    private static readonly Dictionary<Type, bool> DefectTypeCache = new();
    private static readonly Dictionary<Type, string> CardKeyByTypeCache = new();

    public static BdDynamicOddsConfig Config
    {
        get
        {
            if (_config != null) return _config;
            lock (StateLock) return _config ??= LoadConfig();
        }
    }

    public static void InitializeStorage()
    {
        lock (StateLock)
        {
            var config = Config;
            _persistedWeights = LoadWeights();
            _persistedWeights.Normalize(config);
            SaveWeights(_persistedWeights);
            MainFile.Logger.Info($"[BetterDefect] dynamic odds storage ready: config={GetConfigPath()}; state={GetWeightsPath()}; cards={CountCards(_persistedWeights.WeightsByRarity)}, disabled={_persistedWeights.DisabledCards.Count}.");
        }
    }

    public static CardModel TryReplaceRewardCard(Player player, IEnumerable<CardModel> blacklist, CardCreationOptions options, CardModel vanillaCard)
    {
        var config = Config;
        if (!config.Enabled) return vanillaCard;
        if (!IsDefectRun(player) || !IsDefectCard(vanillaCard)) return vanillaCard;

        try
        {
            EnsureActiveSnapshot(player);

            var targetRarity = vanillaCard.Rarity;
            if (targetRarity is CardRarity.Basic or CardRarity.Ancient or CardRarity.Event or CardRarity.Token or CardRarity.None or CardRarity.Curse or CardRarity.Status)
                return vanillaCard;

            var blocked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var blockedCard in blacklist ?? Array.Empty<CardModel>())
            {
                if (blockedCard != null)
                    blocked.Add(CardKey(blockedCard));
            }

            var vanillaKey = CardKey(vanillaCard);
            var vanillaDisabled = _activeDisabledCards.Contains(vanillaKey);
            var uniqueCandidates = new Dictionary<string, CardModel>(StringComparer.Ordinal);
            foreach (var candidate in options.GetPossibleCards(player))
            {
                if (candidate == null) continue;
                if (candidate.Rarity != targetRarity) continue;
                if (!IsDefectCard(candidate)) continue;

                var key = CardKey(candidate);
                if (blocked.Contains(key)) continue;
                if (_activeDisabledCards.Contains(key)) continue;
                if (!uniqueCandidates.ContainsKey(key))
                    uniqueCandidates[key] = candidate;
            }

            var candidates = uniqueCandidates.Values.ToList();

            if (candidates.Count == 0) return vanillaCard;
            if (candidates.Count < 2 && !vanillaDisabled) return vanillaCard;

            if (!vanillaDisabled)
            {
                var min = float.MaxValue;
                var max = float.MinValue;
                foreach (var candidate in candidates)
                {
                    var weight = ActiveWeightFor(candidate, config);
                    if (weight < min) min = weight;
                    if (weight > max) max = weight;
                }
                if (candidates.Count == 0 || max - min < 0.0001f)
                    return vanillaCard;
            }

            var rng = options.RngOverride ?? player.PlayerRng.Rewards;
            var pickedTemplate = candidates.Count == 1 ? candidates[0] : rng.WeightedNextItem(candidates, c => ActiveWeightFor(c, config));
            if (pickedTemplate == null || (!vanillaDisabled && CardKey(pickedTemplate) == vanillaKey)) return vanillaCard;

            var replacement = player.RunState.CreateCard(pickedTemplate, player);
            if (config.LogSelections)
            {
                MainFile.Logger.Info($"[BetterDefect] dynamic odds replaced {SafeId(vanillaCard)} -> {SafeId(replacement)}; rarity={targetRarity}; activeWeight={ActiveWeightFor(pickedTemplate, config):0.###}.");
            }
            return replacement;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] dynamic odds skipped after error: {ex.GetType().Name}: {ex.Message}");
            return vanillaCard;
        }
    }

    public static void RecordRewardChoiceGroup(IEnumerable<CardModel> offeredCards, IEnumerable<CardModel> pickedCards)
    {
        var config = Config;
        if (!config.Enabled) return;

        try
        {
            var offered = offeredCards.Where(c => c != null).GroupBy(CardKey, StringComparer.Ordinal).Select(g => g.First()).ToList();
            var picked = pickedCards.Where(c => c != null).GroupBy(CardKey, StringComparer.Ordinal).Select(g => g.First()).ToList();

            // Only learn from ordinary three-card Defect rewards.  This avoids
            // corrupting the Defect table from colorless rewards, 4-card rewards,
            // mixed-color rewards, and other special reward screens.
            if (offered.Count != 3) return;
            if (offered.Any(c => !IsDefectCard(c) || IsCardDisabled(c))) return;
            if (offered.Any(c => IsUnsupportedRarity(c.Rarity))) return;
            if (picked.Count > 1) return;
            if (picked.Any(p => offered.All(o => CardKey(o) != CardKey(p)))) return;

            Player? owner = null;
            try { owner = offered.Select(c => c.Owner).FirstOrDefault(p => p != null); } catch { }
            if (owner != null && !IsDefectRun(owner)) return;

            if (picked.Count == 0 && Math.Abs(config.SkipGroupDelta) < 0.0001f) return;
            if (picked.Count == 1 && Math.Abs(config.PickDelta) < 0.0001f) return;

            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();

                var changes = new List<string>();
                if (picked.Count == 1)
                {
                    // Picking is exactly one-card +PickDelta.  The matching
                    // negative amount is redistributed only to non-picked cards
                    // of the same rarity, so the picked card does not receive
                    // any extra return from the two unchosen reward cards.
                    var actual = ApplyZeroSumGroupDelta(data, new[] { picked[0] }, config.PickDelta, config);
                    changes.Add($"{SafeId(picked[0])}{actual:+0.###;-0.###;0}");
                }
                else
                {
                    // Skipping/rerolling an ordinary three-Defect-card reward
                    // subtracts SkipGroupDelta across the three offered cards as
                    // a group.  Default: three cards together -0.1, not -0.1 each.
                    var actual = ApplyZeroSumGroupDelta(data, offered, config.SkipGroupDelta, config);
                    changes.Add($"offeredGroup{actual:+0.###;-0.###;0}");
                }

                data.TotalChoices++;
                data.TotalPicked += picked.Count;
                data.TotalSkipped += picked.Count == 0 ? offered.Count : 0;
                data.LastUpdatedUtc = DateTimeOffset.UtcNow;
                SaveWeights(data);

                if (config.LogLearning)
                {
                    MainFile.Logger.Info($"[BetterDefect] dynamic odds strict zero-sum learned ordinary Defect reward; picked={picked.Count}; changes=[{string.Join(", ", changes)}]. Next run only.");
                }
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to record reward choice group: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static float ApplyZeroSumGroupDelta(BdDynamicOddsWeights data, IEnumerable<CardModel> targetCards, float requestedGroupDelta, BdDynamicOddsConfig config)
    {
        var targets = targetCards
            .Where(c => c != null)
            .Where(c => !IsUnsupportedRarity(c.Rarity))
            .GroupBy(CardKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
        if (targets.Count == 0 || Math.Abs(requestedGroupDelta) < 0.0001f) return 0f;

        var perCardRequested = requestedGroupDelta / targets.Count;
        var totalActual = 0f;
        foreach (var rarityGroup in targets.GroupBy(c => c.Rarity))
        {
            totalActual += ApplyZeroSumGroupDeltaForRarity(data, rarityGroup.ToList(), perCardRequested, config);
        }
        return totalActual;
    }

    private static float ApplyZeroSumGroupDeltaForRarity(BdDynamicOddsWeights data, List<CardModel> targets, float perCardRequestedDelta, BdDynamicOddsConfig config)
    {
        if (targets.Count == 0 || Math.Abs(perCardRequestedDelta) < 0.0001f) return 0f;

        var rarity = targets[0].Rarity;
        var rarityKey = RarityKey(rarity);
        if (!data.WeightsByRarity.TryGetValue(rarityKey, out var rarityWeights))
        {
            rarityWeights = new Dictionary<string, float>(StringComparer.Ordinal);
            data.WeightsByRarity[rarityKey] = rarityWeights;
        }

        NormalizeKnownRarityToZeroSum(data, rarity, config);

        var disabled = new HashSet<string>(data.DisabledCards ?? new List<string>(), StringComparer.Ordinal);
        var targetKeys = targets.Select(CardKey).Where(k => !disabled.Contains(k)).Distinct(StringComparer.Ordinal).ToList();
        if (targetKeys.Count == 0) return 0f;

        var enabledKeys = GetEnabledDefectCardKeysForRarity(rarity, data);
        foreach (var key in targetKeys)
            if (!enabledKeys.Contains(key, StringComparer.Ordinal)) enabledKeys.Add(key);
        foreach (var key in enabledKeys)
            if (!rarityWeights.ContainsKey(key)) rarityWeights[key] = config.DefaultWeight;

        var targetSet = new HashSet<string>(targetKeys, StringComparer.Ordinal);
        var recipientKeys = enabledKeys.Where(k => !targetSet.Contains(k)).Distinct(StringComparer.Ordinal).ToList();
        if (recipientKeys.Count == 0) return 0f;

        var actualByTarget = new Dictionary<string, float>(StringComparer.Ordinal);
        var requestedSign = Math.Sign(perCardRequestedDelta);
        var requestedAbs = Math.Abs(perCardRequestedDelta);
        var totalTargetDelta = 0f;

        foreach (var key in targetKeys)
        {
            var before = GetWeight(rarityWeights, key, config);
            var capacity = requestedSign > 0
                ? Math.Max(0f, config.MaxWeight - before)
                : Math.Max(0f, before - config.MinWeight);
            var stepAbs = Math.Min(requestedAbs, capacity);
            if (stepAbs <= 0.0001f)
            {
                actualByTarget[key] = 0f;
                continue;
            }
            var actual = requestedSign * stepAbs;
            actualByTarget[key] = actual;
            totalTargetDelta += actual;
        }

        if (Math.Abs(totalTargetDelta) <= 0.0001f) return 0f;

        var recipientCapacity = totalTargetDelta > 0f
            ? recipientKeys.Sum(k => Math.Max(0f, GetWeight(rarityWeights, k, config) - config.MinWeight))
            : recipientKeys.Sum(k => Math.Max(0f, config.MaxWeight - GetWeight(rarityWeights, k, config)));
        var distributableAbs = Math.Min(Math.Abs(totalTargetDelta), recipientCapacity);
        if (distributableAbs <= 0.0001f) return 0f;

        var scale = distributableAbs / Math.Abs(totalTargetDelta);
        var scaledTotalTargetDelta = 0f;
        foreach (var kv in actualByTarget.ToList())
        {
            var actual = kv.Value * scale;
            if (Math.Abs(actual) <= 0.0001f) continue;
            var before = GetWeight(rarityWeights, kv.Key, config);
            rarityWeights[kv.Key] = Math.Clamp(before + actual, config.MinWeight, config.MaxWeight);
            data.Rarities[kv.Key] = rarityKey;
            scaledTotalTargetDelta += actual;
        }

        DistributeToOthers(rarityWeights, recipientKeys, -scaledTotalTargetDelta, config);
        NormalizeKnownRarityToZeroSum(data, rarity, config);
        return scaledTotalTargetDelta;
    }

    private static void NormalizeAllKnownRaritiesToZeroSum(BdDynamicOddsWeights data, BdDynamicOddsConfig config)
    {
        try
        {
            var rarities = new HashSet<CardRarity>();
            try
            {
                foreach (var card in OldDefectCards.Cards.Where(c => c != null))
                    if (!IsUnsupportedRarity(card.Rarity)) rarities.Add(card.Rarity);
            }
            catch { }
            try
            {
                foreach (var card in ModelDb.AllCards.Where(c => c != null && IsDefectCard(c)))
                    if (!IsUnsupportedRarity(card.Rarity)) rarities.Add(card.Rarity);
            }
            catch { }
            try
            {
                var pool = OldDefectCards.GetDefectPool();
                if (pool != null)
                {
                    foreach (var card in pool.AllCards.Where(c => c != null))
                        if (!IsUnsupportedRarity(card.Rarity)) rarities.Add(card.Rarity);
                }
            }
            catch { }

            foreach (var rarity in rarities)
                NormalizeKnownRarityToZeroSum(data, rarity, config);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to normalize dynamic odds rarity sums: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void NormalizeKnownRarityToZeroSum(BdDynamicOddsWeights data, CardRarity rarity, BdDynamicOddsConfig config)
    {
        var rarityKey = RarityKey(rarity);
        if (!data.WeightsByRarity.TryGetValue(rarityKey, out var rarityWeights))
        {
            rarityWeights = new Dictionary<string, float>(StringComparer.Ordinal);
            data.WeightsByRarity[rarityKey] = rarityWeights;
        }

        var enabledKeys = GetEnabledDefectCardKeysForRarity(rarity, data).Distinct(StringComparer.Ordinal).ToList();
        if (enabledKeys.Count == 0) return;

        foreach (var key in enabledKeys)
        {
            if (!rarityWeights.ContainsKey(key)) rarityWeights[key] = config.DefaultWeight;
            else rarityWeights[key] = Math.Clamp(rarityWeights[key], config.MinWeight, config.MaxWeight);
            data.Rarities[key] = rarityKey;
        }

        var disabled = new HashSet<string>(data.DisabledCards ?? new List<string>(), StringComparer.Ordinal);
        foreach (var key in disabled)
        {
            if (data.Rarities.TryGetValue(key, out var disabledRarity) && string.Equals(disabledRarity, rarityKey, StringComparison.Ordinal))
                rarityWeights[key] = 0f;
        }

        var targetSum = enabledKeys.Count * config.DefaultWeight;
        var currentSum = enabledKeys.Sum(k => GetWeight(rarityWeights, k, config));
        var diff = targetSum - currentSum;
        if (Math.Abs(diff) <= 0.0001f) return;
        DistributeToOthers(rarityWeights, enabledKeys, diff, config);
    }

    private static void DistributeToOthers(Dictionary<string, float> weights, List<string> keys, float totalDelta, BdDynamicOddsConfig config)
    {
        var remaining = Math.Abs(totalDelta);
        var sign = Math.Sign(totalDelta);
        if (sign == 0 || remaining <= 0.0001f) return;

        var active = keys.Distinct(StringComparer.Ordinal).ToList();
        for (var guard = 0; guard < 32 && remaining > 0.0001f && active.Count > 0; guard++)
        {
            var share = remaining / active.Count;
            var next = new List<string>();
            var moved = 0f;

            foreach (var key in active)
            {
                var before = GetWeight(weights, key, config);
                var capacity = sign > 0 ? config.MaxWeight - before : before - config.MinWeight;
                if (capacity <= 0.0001f) continue;

                var step = Math.Min(share, capacity);
                weights[key] = Math.Clamp(before + sign * step, config.MinWeight, config.MaxWeight);
                moved += step;
                if (capacity - step > 0.0001f) next.Add(key);
            }

            if (moved <= 0.0001f) break;
            remaining -= moved;
            active = next;
        }
    }

    private static float GetWeight(Dictionary<string, float> rarityWeights, string key, BdDynamicOddsConfig config)
    {
        return Math.Clamp(rarityWeights.TryGetValue(key, out var value) ? value : config.DefaultWeight, config.MinWeight, config.MaxWeight);
    }

    public static bool ShouldShowWeight(CardModel? card)
    {
        if (card == null) return false;
        try { return IsDefectCard(card); }
        catch { return false; }
    }

    public static float GetDisplayWeight(CardModel? card)
    {
        var config = Config;
        if (card == null) return config.DefaultWeight;

        try
        {
            var rarityKey = RarityKey(card.Rarity);
            var cardKey = CardKey(card);
            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();
                if (data.DisabledCards != null && data.DisabledCards.Contains(cardKey, StringComparer.Ordinal))
                    return 0f;
                if (data.WeightsByRarity.TryGetValue(rarityKey, out var rarityWeights) &&
                    rarityWeights.TryGetValue(cardKey, out var weight))
                {
                    return Math.Clamp(weight, config.MinWeight, config.MaxWeight);
                }
            }
        }
        catch { }

        return config.DefaultWeight;
    }

    public static string GetDisplayLine(CardModel card)
    {
        var weight = GetDisplayWeight(card);
        var suffix = IsCardDisabled(card) ? "（已禁用）" : "";
        return $"[color=#E6C15A]动态出率：{weight:0.00}x{suffix}[/color]";
    }

    public static bool IsCardDisabled(CardModel? card)
    {
        if (card == null) return false;
        try
        {
            if (!IsDefectCard(card)) return false;
            var key = CardKey(card);
            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();
                return data.DisabledCards != null && data.DisabledCards.Contains(key, StringComparer.Ordinal);
            }
        }
        catch { return false; }
    }

    public static int GetDisabledCardCount()
    {
        try
        {
            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();
                data.Normalize(Config);
                return data.DisabledCards
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct(StringComparer.Ordinal)
                    .Count();
            }
        }
        catch { return 0; }
    }

    public static int GetVersionUpgradeCount()
    {
        try
        {
            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();
                data.Normalize(Config);
                return data.UpgradedCards
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct(StringComparer.Ordinal)
                    .Count();
            }
        }
        catch { return 0; }
    }

    public static int GetUsedCardPointCount() => GetDisabledCardCount() + GetVersionUpgradeCount();

    public static bool IsCardVersionUpgraded(CardModel? card)
    {
        if (card == null || !BdCardVersionUpgrades.IsEligible(card)) return false;
        try
        {
            var key = CardKey(card);
            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();
                return data.UpgradedCards != null && data.UpgradedCards.Contains(key, StringComparer.Ordinal);
            }
        }
        catch { return false; }
    }

    public static bool ToggleCardVersionUpgrade(CardModel? card)
    {
        if (card == null || !BdCardVersionUpgrades.IsEligible(card)) return false;
        try
        {
            var key = CardKey(card);
            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();
                var oldRarity = card.Rarity;
                data.UpgradedCards ??= new List<string>();
                var wasUpgraded = data.UpgradedCards.Contains(key, StringComparer.Ordinal);
                if (wasUpgraded)
                {
                    data.UpgradedCards = data.UpgradedCards
                        .Where(k => !string.Equals(k, key, StringComparison.Ordinal))
                        .ToList();
                    MainFile.Logger.Info($"[BetterDefect] card version upgrade disabled for {SafeId(card)}; one card point refunded.");
                }
                else
                {
                    var used = CountUsedCardPoints(data);
                    if (used >= MaxCardPointBudget)
                    {
                        MainFile.Logger.Warn($"[BetterDefect] cannot upgrade {SafeId(card)}: card point budget is full ({used}/{MaxCardPointBudget}).");
                        return false;
                    }

                    data.UpgradedCards.Add(key);
                    MainFile.Logger.Info($"[BetterDefect] card version upgrade enabled for {SafeId(card)}; card points={used + 1}/{MaxCardPointBudget}.");
                }

                var newRarity = card.Rarity;
                MoveWeightToTransformedRarity(data, key, oldRarity, newRarity);
                data.LastUpdatedUtc = DateTimeOffset.UtcNow;
                SaveWeights(data);
                return true;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to toggle card version upgrade: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void MoveWeightToTransformedRarity(
        BdDynamicOddsWeights data,
        string cardKey,
        CardRarity oldRarity,
        CardRarity newRarity)
    {
        if (oldRarity == newRarity || IsUnsupportedRarity(newRarity)) return;
        var oldKey = RarityKey(oldRarity);
        var newKey = RarityKey(newRarity);
        var weight = Config.DefaultWeight;
        if (data.WeightsByRarity.TryGetValue(oldKey, out var oldWeights) &&
            oldWeights.TryGetValue(cardKey, out var oldWeight))
        {
            weight = oldWeight;
            oldWeights.Remove(cardKey);
        }

        if (!data.WeightsByRarity.TryGetValue(newKey, out var newWeights))
        {
            newWeights = new Dictionary<string, float>(StringComparer.Ordinal);
            data.WeightsByRarity[newKey] = newWeights;
        }
        newWeights[cardKey] = data.DisabledCards.Contains(cardKey, StringComparer.Ordinal)
            ? 0f
            : Math.Clamp(weight, Config.MinWeight, Config.MaxWeight);
        data.Rarities[cardKey] = newKey;

        if (!IsUnsupportedRarity(oldRarity))
            NormalizeKnownRarityToZeroSum(data, oldRarity, Config);
        NormalizeKnownRarityToZeroSum(data, newRarity, Config);
    }

    public static bool ToggleCardDisabled(CardModel? card)
    {
        if (card == null) return false;
        try
        {
            if (!IsDefectCard(card)) return false;
            var key = CardKey(card);
            var rarityKey = RarityKey(card.Rarity);
            lock (StateLock)
            {
                var data = _persistedWeights ??= LoadWeights();
                data.DisabledCards ??= new List<string>();
                var wasDisabled = data.DisabledCards.Contains(key, StringComparer.Ordinal);
                if (wasDisabled)
                {
                    data.DisabledCards = data.DisabledCards.Where(k => !string.Equals(k, key, StringComparison.Ordinal)).ToList();
                    data.EnabledStates ??= new Dictionary<string, bool>(StringComparer.Ordinal);
                    data.EnabledStates[key] = true;
                    if (data.WeightsByRarity.TryGetValue(rarityKey, out var rarityWeights) &&
                        rarityWeights.TryGetValue(key, out var oldWeight) && oldWeight <= 0.0001f)
                    {
                        rarityWeights[key] = Config.DefaultWeight;
                    }
                    _activeDisabledCards.Remove(key);
                    MainFile.Logger.Info($"[BetterDefect] dynamic odds re-enabled {SafeId(card)}; reward weight restored.");
                }
                else
                {
                    var used = CountUsedCardPoints(data);
                    if (used >= MaxCardPointBudget)
                    {
                        MainFile.Logger.Warn($"[BetterDefect] cannot disable {SafeId(card)}: card point budget is full ({used}/{MaxCardPointBudget}).");
                        return false;
                    }

                    data.DisabledCards.Add(key);
                    data.EnabledStates ??= new Dictionary<string, bool>(StringComparer.Ordinal);
                    data.EnabledStates[key] = false;
                    if (!data.WeightsByRarity.TryGetValue(rarityKey, out var rarityWeights))
                    {
                        rarityWeights = new Dictionary<string, float>(StringComparer.Ordinal);
                        data.WeightsByRarity[rarityKey] = rarityWeights;
                    }
                    rarityWeights[key] = 0f;
                    data.Rarities[key] = rarityKey;
                    _activeDisabledCards.Add(key);
                    MainFile.Logger.Info($"[BetterDefect] dynamic odds disabled {SafeId(card)}; reward weight set to 0.");
                }
                NormalizeKnownRarityToZeroSum(data, card.Rarity, Config);
                data.LastUpdatedUtc = DateTimeOffset.UtcNow;
                SaveWeights(data);
                return true;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to toggle disabled card: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void EnsureActiveSnapshot(Player player)
    {
        var runState = player.RunState;
        if (ReferenceEquals(_activeRunState, runState)) return;

        lock (StateLock)
        {
            if (ReferenceEquals(_activeRunState, runState)) return;
            _persistedWeights = LoadWeights();
            NormalizeAllKnownRaritiesToZeroSum(_persistedWeights, Config);
            _activeWeightsByRarity = CloneWeights(_persistedWeights.WeightsByRarity);
            _activeDisabledCards = new HashSet<string>(_persistedWeights.DisabledCards ?? new List<string>(), StringComparer.Ordinal);
            _activeRunState = runState;
            MainFile.Logger.Info($"[BetterDefect] dynamic odds snapshot loaded for this run; cards={CountCards(_activeWeightsByRarity)}, picked={_persistedWeights.TotalPicked}, skipped={_persistedWeights.TotalSkipped}. Current choices affect the next run.");
        }
    }

    private static float ActiveWeightFor(CardModel? card, BdDynamicOddsConfig config)
    {
        if (card == null) return config.DefaultWeight;
        var rarityKey = RarityKey(card.Rarity);
        var cardKey = CardKey(card);
        if (_activeDisabledCards.Contains(cardKey)) return 0f;
        if (_activeWeightsByRarity.TryGetValue(rarityKey, out var rarityWeights) && rarityWeights.TryGetValue(cardKey, out var weight))
            return Math.Clamp(weight, config.MinWeight, config.MaxWeight);
        return config.DefaultWeight;
    }

    private static bool IsDefectRun(Player player)
    {
        try { return player.Character.CardPool is DefectCardPool; }
        catch { return player.Character.CardPool.GetType().Name.Contains("Defect", StringComparison.OrdinalIgnoreCase); }
    }

    public static bool IsDefectCard(CardModel card)
    {
        var type = card.GetType();
        lock (TypeCacheLock)
        {
            if (DefectTypeCache.TryGetValue(type, out var cached))
                return cached;
        }

        var result = ComputeIsDefectCard(card);
        lock (TypeCacheLock)
        {
            DefectTypeCache[type] = result;
        }
        return result;
    }

    private static bool ComputeIsDefectCard(CardModel card)
    {
        try { if (OldDefectCards.IsRestored(card)) return true; } catch { }
        try
        {
            var defectPool = OldDefectCards.GetDefectPool();
            if (defectPool != null && ReferenceEquals(card.Pool, defectPool)) return true;
            if (defectPool != null && card.Pool.Id == defectPool.Id) return true;
        }
        catch { }
        try { return card.Pool is DefectCardPool; } catch { }
        return false;
    }

    private static bool IsUnsupportedRarity(CardRarity rarity) =>
        rarity is CardRarity.Basic or CardRarity.Ancient or CardRarity.Event or CardRarity.Token or CardRarity.None or CardRarity.Curse or CardRarity.Status;

    private static string RarityKey(CardRarity rarity) => rarity.ToString();

    private static List<string> GetEnabledDefectCardKeysForRarity(CardRarity rarity, BdDynamicOddsWeights data)
    {
        var disabled = new HashSet<string>(data.DisabledCards ?? new List<string>(), StringComparer.Ordinal);
        var cards = new List<CardModel>();
        try
        {
            var pool = OldDefectCards.GetDefectPool();
            if (pool != null) cards.AddRange(pool.AllCards.Where(c => c != null));
        }
        catch { }
        try { cards.AddRange(OldDefectCards.Cards.Where(c => c != null)); } catch { }
        try { cards.AddRange(ModelDb.AllCards.Where(c => c != null && IsDefectCard(c))); } catch { }

        return cards
            .Where(c => c.Rarity == rarity)
            .Select(CardKey)
            .Where(k => !disabled.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string CardKey(CardModel card)
    {
        var type = card.GetType();
        lock (TypeCacheLock)
        {
            if (CardKeyByTypeCache.TryGetValue(type, out var cached))
                return cached;
        }

        string key;
        try { key = card.CanonicalInstance.Id.ToString(); }
        catch
        {
            try { key = card.Id.ToString(); }
            catch { key = type.FullName ?? type.Name; }
        }

        lock (TypeCacheLock)
        {
            CardKeyByTypeCache[type] = key;
        }
        return key;
    }

    internal static string CardKeyForPatch(CardModel card) => CardKey(card);

    private static string SafeId(CardModel card)
    {
        try { return card.Id.ToString(); } catch { return card.GetType().Name; }
    }

    private static BdDynamicOddsConfig LoadConfig()
    {
        var path = GetConfigPath();
        var shippedPath = GetShippedConfigPath();
        var defaults = new BdDynamicOddsConfig();
        try
        {
            if (!File.Exists(path))
            {
                var initial = defaults;
                if (File.Exists(shippedPath))
                {
                    initial = JsonSerializer.Deserialize<BdDynamicOddsConfig>(File.ReadAllText(shippedPath), JsonOptions) ?? defaults;
                    initial.Normalize();
                    SaveConfig(initial);
                    MainFile.Logger.Info($"[BetterDefect] dynamic odds config migrated to persistent storage: {shippedPath} -> {path}");
                    return initial;
                }

                initial.Normalize();
                SaveConfig(initial);
                MainFile.Logger.Info($"[BetterDefect] dynamic odds config created: {path}");
                return initial;
            }

            var loaded = JsonSerializer.Deserialize<BdDynamicOddsConfig>(File.ReadAllText(path), JsonOptions) ?? defaults;
            loaded.Normalize();
            SaveConfig(loaded);
            MainFile.Logger.Info($"[BetterDefect] dynamic odds enabled={loaded.Enabled}, default={loaded.DefaultWeight:0.###}, pickDelta={loaded.PickDelta:0.###}, skipGroupDelta={loaded.SkipGroupDelta:0.###}, min={loaded.MinWeight:0.###}, max={loaded.MaxWeight:0.###}. Changes apply next run.");
            return loaded;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to load dynamic odds config '{path}', using defaults: {ex.Message}");
            return defaults.Normalize();
        }
    }

    private static BdDynamicOddsWeights LoadWeights()
    {
        var path = GetWeightsPath();
        var legacyPaths = GetLegacyWeightsPaths().ToList();
        try
        {
            if (!File.Exists(path))
            {
                foreach (var legacyPath in legacyPaths)
                {
                    if (!File.Exists(legacyPath)) continue;

                    var migrated = JsonSerializer.Deserialize<BdDynamicOddsWeights>(File.ReadAllText(legacyPath), JsonOptions) ?? new BdDynamicOddsWeights();
                    migrated.Normalize(Config);
                    SaveWeights(migrated);
                    TryDeleteLegacyWeights(legacyPath);
                    MainFile.Logger.Info($"[BetterDefect] migrated dynamic odds probability/enabled state: {legacyPath} -> {path}");
                    return migrated;
                }

                var created = new BdDynamicOddsWeights();
                created.Normalize(Config);
                SaveWeights(created);
                return created;
            }

            var loaded = JsonSerializer.Deserialize<BdDynamicOddsWeights>(File.ReadAllText(path), JsonOptions) ?? new BdDynamicOddsWeights();
            loaded.Normalize(Config);
            foreach (var legacyPath in legacyPaths) TryDeleteLegacyWeights(legacyPath);
            return loaded;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to load dynamic odds weights '{path}', starting from defaults: {ex.Message}");
            return new BdDynamicOddsWeights();
        }
    }

    private static void SaveConfig(BdDynamicOddsConfig config)
    {
        var path = GetConfigPath();
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to save dynamic odds config '{path}': {ex.Message}");
        }
    }

    private static void SaveWeights(BdDynamicOddsWeights data)
    {
        var path = GetWeightsPath();
        try
        {
            data.Normalize(Config);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to save dynamic odds weights '{path}': {ex.Message}");
        }
    }

    private static string GetConfigPath() => Path.Combine(GetDataDirectory(), ConfigFileName);
    private static string GetWeightsPath() => Path.Combine(GetDataDirectory(), WeightsFileName);
    private static string GetShippedConfigPath() => Path.Combine(GetModDirectory(), ConfigFileName);

    private static IEnumerable<string> GetLegacyWeightsPaths()
    {
        var paths = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (string.Equals(path, GetWeightsPath(), StringComparison.OrdinalIgnoreCase)) return;
            if (paths.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
            paths.Add(path);
        }

        try { Add(Path.Combine(GetDataDirectory(), LegacyWeightsFileName)); } catch { }
        try { Add(Path.Combine(GetModDirectory(), WeightsFileName)); } catch { }
        try { Add(Path.Combine(GetModDirectory(), LegacyWeightsFileName)); } catch { }
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                Add(Path.Combine(appData, "SlayTheSpire2", RuntimeFolderName, WeightsFileName));
                Add(Path.Combine(appData, "SlayTheSpire2", RuntimeFolderName, LegacyWeightsFileName));
            }
        }
        catch { }

        return paths;
    }

    private static void TryDeleteLegacyWeights(string legacyPath)
    {
        try
        {
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to delete legacy dynamic odds state '{legacyPath}': {ex.Message}");
        }
    }

    private static string GetDataDirectory()
    {
        try
        {
            var userData = Godot.OS.GetUserDataDir();
            if (!string.IsNullOrWhiteSpace(userData))
                return Path.Combine(userData, RuntimeFolderName);
        }
        catch { }

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
                return Path.Combine(appData, "SlayTheSpire2", RuntimeFolderName);
        }
        catch { }

        try
        {
            var modDir = GetModDirectory();
            if (!string.IsNullOrWhiteSpace(modDir))
                return Path.Combine(modDir, "Data", "Runtime");
        }
        catch { }

        return Environment.CurrentDirectory;
    }

    private static string GetModDirectory()
    {
        try
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            var dir = string.IsNullOrWhiteSpace(loc) ? null : Path.GetDirectoryName(loc);
            if (!string.IsNullOrWhiteSpace(dir)) return dir!;
        }
        catch { }
        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir)) return baseDir;
        }
        catch { }
        return Environment.CurrentDirectory;
    }

    private static Dictionary<string, Dictionary<string, float>> CloneWeights(Dictionary<string, Dictionary<string, float>> source)
    {
        var clone = new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
        foreach (var kv in source)
            clone[kv.Key] = new Dictionary<string, float>(kv.Value, StringComparer.Ordinal);
        return clone;
    }

    private static int CountCards(Dictionary<string, Dictionary<string, float>> source) => source.Values.Sum(v => v.Count);

    private static int CountUsedCardPoints(BdDynamicOddsWeights data)
    {
        var disabled = data.DisabledCards?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).Count() ?? 0;
        var upgraded = data.UpgradedCards?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).Count() ?? 0;
        return disabled + upgraded;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
}

internal sealed class BdDynamicOddsConfig
{
    public bool Enabled { get; set; } = true;
    public float DefaultWeight { get; set; } = 1.0f;
    public float PickDelta { get; set; } = 0.1f;
    public float SkipGroupDelta { get; set; } = -0.1f;
    // Kept for compatibility with older config files; zero-sum learning uses
    // SkipGroupDelta and only applies to ordinary three-card Defect rewards.
    public float SkipDelta { get; set; } = -0.05f;
    public float MinWeight { get; set; } = 0.5f;
    public float MaxWeight { get; set; } = 2.0f;
    public bool LogSelections { get; set; } = false;
    public bool LogLearning { get; set; } = false;

    public BdDynamicOddsConfig Normalize()
    {
        if (float.IsNaN(MinWeight) || MinWeight <= 0f) MinWeight = 0.5f;
        if (float.IsNaN(MaxWeight) || MaxWeight <= 0f) MaxWeight = 2.0f;
        if (MaxWeight < MinWeight) MaxWeight = MinWeight;
        if (float.IsNaN(DefaultWeight) || DefaultWeight <= 0f) DefaultWeight = 1.0f;
        DefaultWeight = Math.Clamp(DefaultWeight, MinWeight, MaxWeight);
        if (float.IsNaN(PickDelta)) PickDelta = 0.1f;
        if (float.IsNaN(SkipGroupDelta)) SkipGroupDelta = -0.1f;
        if (float.IsNaN(SkipDelta)) SkipDelta = -0.05f;
        return this;
    }
}

internal sealed class BdDynamicOddsWeights
{
    private const string RemovedAmplifyId = "CARD.BD_AMPLIFY";

    public int Version { get; set; } = 4;
    public Dictionary<string, Dictionary<string, float>> WeightsByRarity { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Rarities { get; set; } = new(StringComparer.Ordinal);
    public List<string> DisabledCards { get; set; } = new();
    public Dictionary<string, bool> EnabledStates { get; set; } = new(StringComparer.Ordinal);
    public List<string> UpgradedCards { get; set; } = new();
    public int TotalChoices { get; set; }
    public int TotalPicked { get; set; }
    public int TotalSkipped { get; set; }
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public void Normalize(BdDynamicOddsConfig config)
    {
        WeightsByRarity ??= new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
        Rarities ??= new Dictionary<string, string>(StringComparer.Ordinal);
        DisabledCards ??= new List<string>();
        EnabledStates ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        UpgradedCards ??= new List<string>();
        Version = Math.Max(Version, 4);

        // v0.10.12 replaces the restored StS1 Amplify with Seek. Purge the
        // removed card from every persisted table so an invisible Amplify
        // disable entry cannot keep consuming one of the 50 card points.
        foreach (var weights in WeightsByRarity.Values.Where(weights => weights != null))
            weights.Remove(RemovedAmplifyId);
        Rarities.Remove(RemovedAmplifyId);
        DisabledCards.RemoveAll(key => string.Equals(key, RemovedAmplifyId, StringComparison.Ordinal));
        EnabledStates.Remove(RemovedAmplifyId);
        UpgradedCards.RemoveAll(key => string.Equals(key, RemovedAmplifyId, StringComparison.Ordinal));

        foreach (var key in EnabledStates.Keys.ToList())
        {
            if (string.IsNullOrWhiteSpace(key))
                EnabledStates.Remove(key);
        }

        DisabledCards = DisabledCards.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList();
        UpgradedCards = UpgradedCards.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList();

        foreach (var kv in EnabledStates.ToList())
        {
            if (kv.Value)
                DisabledCards = DisabledCards.Where(k => !string.Equals(k, kv.Key, StringComparison.Ordinal)).ToList();
            else if (!DisabledCards.Contains(kv.Key, StringComparer.Ordinal))
                DisabledCards.Add(kv.Key);
        }

        foreach (var key in DisabledCards)
            EnabledStates[key] = false;

        var disabled = new HashSet<string>(DisabledCards, StringComparer.Ordinal);
        foreach (var rarity in WeightsByRarity.Keys.ToList())
        {
            if (WeightsByRarity[rarity] == null)
            {
                WeightsByRarity[rarity] = new Dictionary<string, float>(StringComparer.Ordinal);
                continue;
            }

            foreach (var cardKey in WeightsByRarity[rarity].Keys.ToList())
                WeightsByRarity[rarity][cardKey] = disabled.Contains(cardKey) ? 0f : Math.Clamp(WeightsByRarity[rarity][cardKey], config.MinWeight, config.MaxWeight);
        }
    }
}

[HarmonyPatch(typeof(CardFactory), "CreateForReward", new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardCreationOptions) })]
internal static class BdDynamicRewardOddsPatch
{
    private static void Postfix(Player player, IEnumerable<CardModel> blacklist, CardCreationOptions options, ref CardModel __result)
    {
        __result = BdDynamicOdds.TryReplaceRewardCard(player, blacklist, options, __result);
    }
}

internal sealed class BdRewardChoiceState
{
    public CardReward Reward { get; }
    public List<CardModel> Offered { get; }

    private BdRewardChoiceState(CardReward reward, List<CardModel> offered)
    {
        Reward = reward;
        Offered = offered;
    }

    public static BdRewardChoiceState Capture(CardReward reward)
    {
        try { return new BdRewardChoiceState(reward, reward.Cards.Where(c => c != null).ToList()); }
        catch { return new BdRewardChoiceState(reward, new List<CardModel>()); }
    }
}

[HarmonyPatch]
internal static class BdDynamicCardRewardOnSelectPatch
{
    private static MethodBase? TargetMethod() => AccessTools.Method(typeof(CardReward), "OnSelect");

    private static void Prefix(CardReward __instance, out BdRewardChoiceState __state)
    {
        __state = BdRewardChoiceState.Capture(__instance);
    }

    private static void Postfix(BdRewardChoiceState __state, ref Task<bool> __result)
    {
        var original = __result;
        __result = AfterSelect(original, __state);
    }

    private static async Task<bool> AfterSelect(Task<bool> original, BdRewardChoiceState state)
    {
        var rewardComplete = await original;
        if (!rewardComplete) return rewardComplete;

        try
        {
            var remainingKeys = state.Reward.Cards.Select(BdDynamicOdds.CardKeyForPatch).ToHashSet(StringComparer.Ordinal);
            var picked = state.Offered.Where(c => !remainingKeys.Contains(BdDynamicOdds.CardKeyForPatch(c))).ToList();
            BdDynamicOdds.RecordRewardChoiceGroup(state.Offered, picked);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] dynamic odds OnSelect group failed: {ex.GetType().Name}: {ex.Message}");
        }

        return rewardComplete;
    }
}

[HarmonyPatch(typeof(CardReward), nameof(CardReward.OnSkipped))]
internal static class BdDynamicCardRewardOnSkippedPatch
{
    private static void Prefix(CardReward __instance, out BdRewardChoiceState __state)
    {
        __state = BdRewardChoiceState.Capture(__instance);
    }

    private static void Postfix(BdRewardChoiceState __state)
    {
        BdDynamicOdds.RecordRewardChoiceGroup(__state.Offered, Array.Empty<CardModel>());
    }
}

[HarmonyPatch(typeof(CardReward), nameof(CardReward.Reroll))]
internal static class BdDynamicCardRewardRerollPatch
{
    private static void Prefix(CardReward __instance, out BdRewardChoiceState __state)
    {
        __state = BdRewardChoiceState.Capture(__instance);
    }

    private static void Postfix(BdRewardChoiceState __state)
    {
        BdDynamicOdds.RecordRewardChoiceGroup(__state.Offered, Array.Empty<CardModel>());
    }
}
