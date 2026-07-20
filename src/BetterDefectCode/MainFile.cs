using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BetterDefect;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "BetterDefect";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var harmony = new Harmony(ModId);
        var android = IsAndroidRuntime();
        var patchTypes = new List<Type>();
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type == typeof(BdLocExistsPatch) || type == typeof(BdLocRawPatch))
            {
                Logger.Warn($"[BetterDefect] skipping obsolete {type.FullName}; localization is merged through LocManager instead of detouring LocString.");
                continue;
            }
            if (android && type == typeof(BdDynamicCardRewardRerollPatch))
            {
                // CardReward.Reroll's ARM64 MonoMod trampoline intermittently
                // segfaults during PatchAll on v0.103.2. Normal three-card
                // rewards do not use this method, so keep pick/skip accounting
                // and omit only reroll-skip accounting on Android.
                Logger.Warn($"[BetterDefect] skipping Android-unsafe {type.FullName}; normal reward pick/skip odds remain enabled.");
                continue;
            }
            if (android && IsRedundantAndroidCardLibraryPatch(type))
            {
                // On Android the encyclopedia is driven by InitGrid and
                // AssignCardsToRow, while its own watcher handles temporary
                // hide/show transitions. Avoid redundant NCard/NSubmenu hooks:
                // each native ARM64 trampoline increases startup fragility and
                // these callbacks add no behavior that the two grid hooks and
                // the explicit close hook do not already cover.
                Logger.Warn($"[BetterDefect] skipping redundant Android card-library hook {type.FullName}.");
                continue;
            }
            if (android && type == typeof(BdCardVersionModelDbInitPatch))
            {
                Logger.Warn($"[BetterDefect] skipping merged Android startup hook {type.FullName}.");
                continue;
            }
            patchTypes.Add(type);
        }

        foreach (var type in patchTypes)
        {
            Logger.Info($"[BetterDefect] patching {type.FullName}");
            harmony.CreateClassProcessor(type).Patch();
            Logger.Info($"[BetterDefect] patched {type.FullName}");
        }
        BdDynamicOdds.InitializeStorage();
        BdLocalization.MergeIntoLocManager();
        BdDynamicOddsStatsHud.EnsureInstalled();
        Logger.Info("[BetterDefect] loaded v0.9.0: 14 historical versions and 12 custom card transformations share the persistent 35-point Encyclopedia budget; controls remain strictly bound to the active card grid; BaseLib not required.");
    }

    private static bool IsAndroidRuntime()
    {
        try
        {
            if (OS.HasFeature("android")) return true;
            if (string.Equals(OS.GetName(), "Android", StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { }

        try
        {
            if (!OperatingSystem.IsWindows() && Directory.Exists("/data/data") && Directory.Exists("/system")) return true;
        }
        catch { }

        return false;
    }

    private static bool IsRedundantAndroidCardLibraryPatch(Type type) =>
        type == typeof(BdDynamicOddsCardLibraryOpenedPatch) ||
        type == typeof(BdDynamicOddsCardLibraryFilterPatch) ||
        type == typeof(BdDynamicOddsCardLibraryClosedPatch) ||
        type == typeof(BdDynamicOddsCardLibraryFinalFilterPatch) ||
        type == typeof(BdDynamicOddsCardLibraryGridInitPatch) ||
        type == typeof(BdDynamicOddsCardLibraryGridAssignPatch) ||
        type == typeof(BdDynamicOddsCardLibraryUpgradePreviewPatch) ||
        type == typeof(BdDynamicOddsCardLibraryVisibilityPatch) ||
        type == typeof(BdDynamicOddsCardModelSetPatch) ||
        type == typeof(BdDynamicOddsCardReloadPatch) ||
        type == typeof(BdDynamicOddsCardUpdateVisualsScopePatch) ||
        type == typeof(BdDynamicOddsCardExitTreeScopePatch);
}



