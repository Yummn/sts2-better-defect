using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using BetterDefect.Cards;

namespace BetterDefect;

/// <summary>
/// BetterDefect power models have BD-prefixed ids, so the base game looks for
/// BD_* atlas sprites that do not exist and displays its red "NOPE" missing
/// icon. Redirect those paths to an existing semantically matching power icon.
/// The original Heatsinks icon is preferred where the game ships it; every
/// other mapping uses a stable built-in fallback available on v103 and v107.
/// </summary>
[HarmonyPatch]
internal static class BdPowerIconPathPatch
{
    internal static readonly Dictionary<Type, string[]> IconEntries = new()
    {
        [typeof(BdHeatsinksPower)] = new[] { "HEATSINKS_POWER", "FOCUS_POWER" },
        [typeof(BdSelfRepairPower)] = new[] { "REGEN_POWER" },
        [typeof(BdStaticDischargePower)] = new[] { "THORNS_POWER", "LIGHTNING_ROD_POWER" },
        [typeof(BdElectrodynamicsPower)] = new[] { "LIGHTNING_ROD_POWER", "FOCUS_POWER" },
        [typeof(BdLockOnPower)] = new[] { "VULNERABLE_POWER" },
    };

    private static readonly Dictionary<(Type Type, bool Big), string> ResolvedPaths = new();
    private static readonly HashSet<(Type Type, bool Big)> MissingPaths = new();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var packed = AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.PackedIconPath));
        // BigIconPath is public in mobile v103 but is no longer exposed by the
        // current PC reference assembly. Resolve it by name so one binary source
        // remains compatible with both API surfaces.
        var big = AccessTools.PropertyGetter(typeof(PowerModel), "BigIconPath");
        if (packed != null) yield return packed;
        // The reported defect is the small combat status icon. Keep Android at
        // one new native getter detour; PC can safely fix the large tooltip icon
        // as well. This balances the skipped beta-portrait getter on v103.
        if (!MainFile.IsAndroidRuntime() && big != null) yield return big;
    }

    private static void Postfix(PowerModel __instance, MethodBase __originalMethod, ref string __result)
    {
        var type = __instance.GetType();
        if (!IconEntries.ContainsKey(type)) return;

        var big = __originalMethod.Name.Contains("BigIconPath", StringComparison.Ordinal);
        var key = (type, big);
        if (ResolvedPaths.TryGetValue(key, out var cached))
        {
            __result = cached;
            return;
        }
        if (MissingPaths.Contains(key)) return;

        foreach (var entry in IconEntries[type])
        {
            var relative = big
                ? $"powers/{entry.ToLowerInvariant()}.png"
                : $"atlases/power_atlas.sprites/{entry.ToLowerInvariant()}.tres";
            var candidate = ImageHelper.GetImagePath(relative);
            if (!ResourceLoader.Exists(candidate)) continue;

            ResolvedPaths[key] = candidate;
            __result = candidate;
            MainFile.Logger.Info($"[BetterDefect] power icon mapped: {type.Name} -> {entry} ({(big ? "big" : "status")}).");
            return;
        }

        MissingPaths.Add(key);
        MainFile.Logger.Warn($"[BetterDefect] no compatible power icon found for {type.Name}; keeping engine fallback path {__result}.");
    }

    internal static bool TryResolveStatusPath(PowerModel power, out string path)
    {
        var type = power.GetType();
        var key = (type, false);
        if (ResolvedPaths.TryGetValue(key, out path!)) return true;
        if (!IconEntries.TryGetValue(type, out var entries)) return false;

        foreach (var entry in entries)
        {
            var candidate = ImageHelper.GetImagePath($"atlases/power_atlas.sprites/{entry.ToLowerInvariant()}.tres");
            if (!ResourceLoader.Exists(candidate)) continue;
            ResolvedPaths[key] = candidate;
            path = candidate;
            MainFile.Logger.Info($"[BetterDefect] power icon mapped: {type.Name} -> {entry} (status texture).");
            return true;
        }

        path = string.Empty;
        return false;
    }

    internal static void ValidateInjectedStatusIcons()
    {
        var getByType = AccessTools.Method(typeof(ModelDb), "Get", new[] { typeof(Type) });
        foreach (var type in IconEntries.Keys)
        {
            try
            {
                var power = getByType?.Invoke(null, new object[] { type }) as PowerModel;
                if (power == null)
                {
                    MainFile.Logger.Warn($"[BetterDefect] could not validate status icon for {type.Name}: injected model not found.");
                    continue;
                }
                _ = power.Icon;
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"[BetterDefect] status icon validation failed for {type.Name}: {ex.GetBaseException().Message}");
            }
        }
    }
}

/// <summary>
/// Android v103 inlines PowerModel.PackedIconPath while resolving Icon, which
/// bypasses a getter detour. Patch the final texture getter on Android instead.
/// MainFile excludes this patch on PC and excludes the path patch on Android.
/// </summary>
[HarmonyPatch(typeof(PowerModel), "get_Icon")]
internal static class BdPowerIconTexturePatch
{
    private static void Postfix(PowerModel __instance, ref Texture2D __result)
    {
        if (!BdPowerIconPathPatch.TryResolveStatusPath(__instance, out var path)) return;
        var texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
        if (texture != null) __result = texture;
    }
}
