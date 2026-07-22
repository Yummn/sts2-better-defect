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
                // Android drives the encyclopedia UI entirely from the
                // lightweight LibraryWatcher. Avoid all native card-library
                // trampolines: every extra ARM64 detour increases startup
                // fragility, while the watcher can apply, remove and refresh
                // the same controls without patching game UI methods.
                Logger.Warn($"[BetterDefect] skipping redundant Android card-library hook {type.FullName}.");
                continue;
            }
            if (android && type == typeof(BdCardVersionModelDbInitPatch))
            {
                Logger.Warn($"[BetterDefect] skipping merged Android startup hook {type.FullName}.");
                continue;
            }
            if (android && type == typeof(BetterDefectBetaPortraitPatch))
            {
                // v103's ARM64 Harmony backend becomes unstable when too many
                // property getters are detoured during startup. The normal
                // portrait getter already supplies BetterDefect art on Android;
                // omitting only the beta getter frees one trampoline for the
                // combat power status-icon fix without changing card faces.
                Logger.Warn($"[BetterDefect] skipping redundant Android beta-portrait hook {type.FullName}.");
                continue;
            }
            if (android && type == typeof(BdPowerIconPathPatch))
            {
                // Calls to PackedIconPath are inlined by the Android runtime,
                // so patch the final Texture2D getter instead.
                continue;
            }
            if (android && type.FullName == "BetterDefect.Cards.BdElectrodynamicsLightningTargetPatch")
            {
                // LightningOrb.ApplyLightningDamage is an ARM64-crashy detour
                // point on v103.  Skipping this one effect hook is preferable
                // to aborting process startup before the encyclopedia UI and
                // reward-odds systems can load.
                Logger.Warn($"[BetterDefect] skipping Android-unsafe {type.FullName}; Electrodynamics all-target lightning fallback is disabled on mobile.");
                continue;
            }
            if (!android && type == typeof(BdPowerIconTexturePatch))
            {
                // PC can redirect both small and large paths before loading.
                continue;
            }
            patchTypes.Add(type);
        }

        foreach (var type in patchTypes)
        {
            try
            {
                Logger.Info($"[BetterDefect] patching {type.FullName}");
                harmony.CreateClassProcessor(type).Patch();
                Logger.Info($"[BetterDefect] patched {type.FullName}");
            }
            catch (Exception ex)
            {
                // Do not let one cross-version card hook abort the rest of the
                // mod initializer.  On Android v103 a single missing renamed
                // method previously stopped the encyclopedia watcher/HUD from
                // being installed, which made the point bar and all in-card
                // controls disappear even though the mod was listed as loaded.
                Logger.Warn($"[BetterDefect] patch skipped after failure in {type.FullName}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        BdDynamicOdds.InitializeStorage();
        BdLocalization.MergeIntoLocManager();
        BdDynamicOddsStatsHud.EnsureInstalled();
        Logger.Info("[BetterDefect] loaded v0.10.5: encyclopedia controls are suppressed while the card-detail overlay is open; Android omits the redundant library-open detour.");
    }

    internal static bool IsAndroidRuntime()
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
        type == typeof(BdDynamicOddsCardLibraryClosedPatch) ||
        type == typeof(BdDynamicOddsCardLibraryFilterPatch) ||
        type == typeof(BdDynamicOddsCardLibraryFinalFilterPatch) ||
        type == typeof(BdDynamicOddsCardLibraryGridAssignPatch) ||
        type == typeof(BdDynamicOddsCardLibraryGridInitPatch) ||
        type == typeof(BdDynamicOddsCardLibraryOpenedPatch) ||
        type == typeof(BdDynamicOddsCardLibraryUpgradePreviewPatch) ||
        type == typeof(BdDynamicOddsCardLibraryVisibilityPatch) ||
        type == typeof(BdDynamicOddsCardModelSetPatch) ||
        type == typeof(BdDynamicOddsCardReloadPatch) ||
        type == typeof(BdDynamicOddsCardUpdateVisualsScopePatch) ||
        type == typeof(BdDynamicOddsCardExitTreeScopePatch);
}



