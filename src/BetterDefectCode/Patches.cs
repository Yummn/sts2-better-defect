using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;

namespace BetterDefect;

internal static class BetterDefectPortraitCache
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, bool> ExistsByPath = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> FixedPathByCardAndPath = new(StringComparer.Ordinal);

    public static string Resolve(CardModel card, string? path)
    {
        if (!OldDefectCards.IsRestored(card))
            return path ?? CardModel.MissingPortraitPath;

        var typeName = card.GetType().FullName ?? card.GetType().Name;
        var key = typeName + "\n" + (path ?? "");
        lock (Lock)
        {
            if (FixedPathByCardAndPath.TryGetValue(key, out var cached))
                return cached;
        }

        var fixedPath = IsUsableResourcePath(path) ? path! : CardModel.MissingPortraitPath;
        lock (Lock)
        {
            FixedPathByCardAndPath[key] = fixedPath;
        }
        return fixedPath;
    }

    private static bool IsUsableResourcePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        lock (Lock)
        {
            if (ExistsByPath.TryGetValue(path, out var cached))
                return cached;
        }

        var exists = false;
        try { exists = ResourceLoader.Exists(path); }
        catch { exists = false; }

        lock (Lock)
        {
            ExistsByPath[path] = exists;
        }
        return exists;
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
internal static class BetterDefectModelDbInitPatch
{
    private static void Postfix() => OldDefectCards.EnsureInjected();
}

[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
internal static class DefectCardPoolUnlockedCardsPatch
{
    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance is MegaCrit.Sts2.Core.Models.CardPools.DefectCardPool)
        {
            try { __result = OldDefectCards.AppendTo(__result); }
            catch (Exception ex) { MainFile.Logger.Error($"[BetterDefect] failed while extending unlocked Defect cards: {ex}"); }
        }
    }
}

[HarmonyPatch(typeof(DefectCardPool), "GenerateAllCards")]
internal static class DefectCardPoolGenerateAllCardsPatch
{
    private static bool _logged;

    private static void Postfix(ref CardModel[] __result)
    {
        try
        {
            var before = __result?.Length ?? 0;
            __result = OldDefectCards.AppendToArray(__result ?? Array.Empty<CardModel>());
            if (!_logged)
            {
                _logged = true;
                MainFile.Logger.Info($"[BetterDefect] Defect GenerateAllCards expanded {before} -> {__result.Length}.");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[BetterDefect] failed while extending Defect GenerateAllCards: {ex}");
        }
    }
}

[HarmonyPatch]
internal static class OldDefectCardPoolPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        OldDefectCards.Types.Select(t => AccessTools.PropertyGetter(t, nameof(CardModel.Pool))).Where(m => m != null).Distinct()!;

    private static bool Prefix(CardModel __instance, ref CardPoolModel __result)
    {
        if (OldDefectCards.IsRestored(__instance))
        {
            var pool = OldDefectCards.GetDefectPool();
            if (pool != null)
            {
                __result = pool;
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch]
internal static class OldDefectCardRarityPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        OldDefectCards.Types.Select(t => AccessTools.PropertyGetter(t, nameof(CardModel.Rarity))).Where(m => m != null).Distinct()!;

    private static void Postfix(CardModel __instance, ref CardRarity __result)
    {
        if (OldDefectCards.TryGetRarity(__instance, out var rarity)) __result = rarity;
    }
}

[HarmonyPatch(typeof(CardModel), "get_PortraitPath")]
internal static class BetterDefectPortraitPatch
{
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try { __result = BetterDefectPortraitCache.Resolve(__instance, __result); }
        catch { __result = CardModel.MissingPortraitPath; }
    }
}

[HarmonyPatch(typeof(CardModel), "get_BetaPortraitPath")]
internal static class BetterDefectBetaPortraitPatch
{
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try { __result = BetterDefectPortraitCache.Resolve(__instance, __result); }
        catch { __result = CardModel.MissingPortraitPath; }
    }
}

internal sealed class BdCombatStats
{
    public int LightningChanneled;
    public int FrostChanneled;
    public int PowerCardsPlayed;
}

internal static class BdCombatTracker
{
    private static readonly Dictionary<Player, BdCombatStats> StatsByPlayer = new();
    public static BdCombatStats For(Player player)
    {
        if (!StatsByPlayer.TryGetValue(player, out var s)) StatsByPlayer[player] = s = new BdCombatStats();
        return s;
    }
    public static void Clear(Player player) => StatsByPlayer.Remove(player);
}

[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.Channel), new[] { typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(OrbModel), typeof(Player) })]
internal static class BdOrbChannelTrackerPatch
{
    private static void Prefix(OrbModel orb, Player player)
    {
        var s = BdCombatTracker.For(player);
        if (orb is LightningOrb) s.LightningChanneled++;
        if (orb is FrostOrb) s.FrostChanneled++;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class BdPowerPlayTrackerPatch
{
    private static void Prefix(CardModel __instance)
    {
        try
        {
            if (__instance.Type == CardType.Power && __instance.Owner != null)
                BdCombatTracker.For(__instance.Owner).PowerCardsPlayed++;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.AfterCombatEnd))]
internal static class BdCombatEndTrackerPatch
{
    private static void Prefix(PlayerCombatState __instance)
    {
        try
        {
            var field = AccessTools.Field(typeof(PlayerCombatState), "_player");
            if (field?.GetValue(__instance) is Player p) BdCombatTracker.Clear(p);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize))]
internal static class BdLocManagerInitializePatch
{
    private static void Postfix() => BdLocalization.MergeIntoLocManager();
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
internal static class BdLocManagerSetLanguagePatch
{
    private static void Postfix() => BdLocalization.MergeIntoLocManager();
}

[HarmonyPatch(typeof(LocString), nameof(LocString.Exists), typeof(string), typeof(string))]
internal static class BdLocExistsPatch
{
    private static bool Prefix(string table, string key, ref bool __result)
    {
        if (BdLocalization.TryGetRaw(table, key, out _)) { __result = true; return false; }
        return true;
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class BdLocRawPatch
{
    private static bool Prepare()
    {
        // Android/Harmony DMD occasionally segfaults while detouring
        // LocString.GetRawText on v0.103.2.  LocString.Exists remains patched so
        // our custom keys are recognized; skipping this detour is preferable to
        // a startup crash.
        return !string.Equals(Godot.OS.GetName(), "Android", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Prefix(LocString __instance, ref string __result)
    {
        if (BdLocalization.TryGetRaw(__instance.LocTable, __instance.LocEntryKey, out var raw))
        {
            __result = raw;
            return false;
        }
        return true;
    }
}
