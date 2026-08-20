using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

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
        var androidCardBridgeInstalled = android && TryInstallAndroidCardPlayBridge();
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
            if (android && type == typeof(DefectCardPoolUnlockedCardsPatch))
            {
                // DefectCardPool.GetUnlockedCards ultimately consumes
                // GenerateAllCards, which is already extended by
                // DefectCardPoolGenerateAllCardsPatch. Detouring the inherited
                // GetUnlockedCards method a second time is redundant on v103
                // and can push the ARM64 MonoMod backend past its stable
                // trampoline budget during startup.
                Logger.Warn($"[BetterDefect] skipping redundant Android pool hook {type.FullName}; GenerateAllCards remains extended.");
                continue;
            }
            if (android && (type == typeof(OldDefectCardPoolPatch) ||
                            type == typeof(OldDefectCardRarityPatch)))
            {
                // Both are virtual property-getter detours and are the final
                // recurring source of ARM64 SIGABRT/SIGSEGV during v103 cold
                // starts. Restored cards are already appended to the Defect
                // pool and construct with their correct rarity. Optional
                // rarity transformations write CardModel's backing field
                // directly, so neither native getter trampoline is needed.
                Logger.Warn($"[BetterDefect] skipping Android-unsafe metadata getter {type.FullName}; pool membership and transformed rarity use detour-free data updates.");
                continue;
            }
            if (android && (type == typeof(BdCustomBeamCellHoverTipsPatch) ||
                            type == typeof(BdCustomFightThroughHoverTipsPatch)))
            {
                // These two patches only add duplicate keyword tooltips; the
                // transformed card descriptions already contain the same
                // information. Keep the mobile build lean by avoiding two
                // non-gameplay ARM64 detours.
                Logger.Warn($"[BetterDefect] skipping nonessential Android tooltip hook {type.FullName}.");
                continue;
            }
            if (android && type == typeof(BdPowerIconPathPatch))
            {
                // Calls to PackedIconPath are inlined by the Android runtime,
                // so patch the final Texture2D getter instead.
                continue;
            }
            if (!android && type == typeof(BdPowerIconTexturePatch))
            {
                // PC can redirect both small and large paths before loading.
                continue;
            }
            if (!android && type == typeof(BdAndroidCentralCardPlayPatch))
            {
                // PC's Harmony backend is stable and retains the existing
                // per-card patches. The central async-state-machine route is
                // only needed to reduce ARM64 native detours on v103.
                continue;
            }
            if (android && androidCardBridgeInstalled && type == typeof(BdAndroidCentralCardPlayPatch))
            {
                Logger.Info("[BetterDefect] Android core card-play bridge active; Harmony async-state-machine transpiler is not needed.");
                continue;
            }
            if (android && IsReplacedByAndroidCentralCardPlayPatch(type))
            {
                Logger.Warn($"[BetterDefect] skipping per-card Android hook {type.FullName}; behavior is routed through one central OnPlay dispatcher.");
                continue;
            }
            patchTypes.Add(type);
        }

        BdCardUpgradeState.InitializeStorage();
        BdLocalization.MergeIntoLocManager();
        BdCardUpgradeStatsHud.EnsureInstalled();

        if (android && TryScheduleAndroidPatches(harmony, patchTypes))
        {
            Logger.Info($"[BetterDefect] loaded v0.11.62: Android v103/v110 compatibility build; transformed Stack inherits the former Recycle orb-slot effect, while transformed Recycle refunds the exhausted card's current cost without exhausting itself; startup-safe patch queue scheduled ({patchTypes.Count} classes).");
            return;
        }

        foreach (var type in patchTypes)
        {
            PatchOne(harmony, type);
        }
            Logger.Info("[BetterDefect] loaded v0.11.62: PC v107.1 compatibility build; transformed Stack and Recycle effects have been reassigned.");
    }

    private static bool TryInstallAndroidCardPlayBridge()
    {
        try
        {
            var bridgeType = typeof(MegaCrit.Sts2.Core.Models.CardModel).Assembly.GetType(
                "MegaCrit.Sts2.Core.Modding.AndroidCardPlayBridge",
                throwOnError: false);
            var handlerField = bridgeType?.GetField(
                "Handler",
                BindingFlags.Public | BindingFlags.Static);
            var dispatcher = typeof(BdAndroidCardPlayDispatcher).GetMethod(
                nameof(BdAndroidCardPlayDispatcher.TryOnPlay),
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (handlerField is null || dispatcher is null)
                return false;

            handlerField.SetValue(null, dispatcher);
            Logger.Info("[BetterDefect] Android core card-play bridge registered; transformed cards use reflection-safe dispatch and normal cards retain native virtual OnPlay.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[BetterDefect] Android core card-play bridge unavailable; using Harmony fallback: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool TryScheduleAndroidPatches(Harmony harmony, IReadOnlyList<Type> patchTypes)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root is null)
                return false;

            var installer = new AndroidPatchInstaller();
            installer.Configure(harmony, patchTypes);
            tree.Root.CallDeferred("add_child", installer);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[BetterDefect] Android deferred patch queue unavailable; falling back to synchronous install: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    internal static void PatchOne(Harmony harmony, Type type)
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
            // mod initializer. On Android v103 a single missing renamed method
            // previously stopped the encyclopedia watcher/HUD from loading.
            Logger.Warn($"[BetterDefect] patch skipped after failure in {type.FullName}: {ex}");
        }
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
        type == typeof(BdCardUpgradeLibraryClosedPatch) ||
        type == typeof(BdCardUpgradeLibraryFilterPatch) ||
        type == typeof(BdCardUpgradeLibraryFinalFilterPatch) ||
        type == typeof(BdCardUpgradeLibraryGridAssignPatch) ||
        type == typeof(BdCardUpgradeLibraryGridInitPatch) ||
        type == typeof(BdCardUpgradeLibraryOpenedPatch) ||
        type == typeof(BdCardUpgradeLibraryUpgradePreviewPatch) ||
        type == typeof(BdCardUpgradeLibraryVisibilityPatch) ||
        type == typeof(BdCardUpgradeModelSetPatch) ||
        type == typeof(BdCardUpgradeReloadPatch) ||
        type == typeof(BdCardUpgradeUpdateVisualsScopePatch) ||
        type == typeof(BdCardUpgradeExitTreeScopePatch);

    private static bool IsReplacedByAndroidCentralCardPlayPatch(Type type) =>
        type == typeof(BdPowerPlayTrackerPatch) ||
        type == typeof(BdCustomCommonCardPlayPatch) ||
        type == typeof(BdCustomRareAdaptiveStrikePlayPatch) ||
        type == typeof(BdCustomRareAllForOnePlayPatch) ||
        type == typeof(BdCustomRareBufferPlayPatch) ||
        type == typeof(BdCustomRareFlakCannonPlayPatch) ||
        type == typeof(BdCustomRareMeteorStrikePlayPatch) ||
        type == typeof(BdCustomRareMultiCastPlayPatch) ||
        type == typeof(BdCustomRareRainbowPlayPatch) ||
        type == typeof(BdCardVersionShatterPlayPatch) ||
        type == typeof(BdCardVersionTeslaCoilPlayPatch) ||
        type == typeof(BdCardVersionFuelPlayPatch) ||
        type == typeof(BdCardVersionScrapePlayPatch);
}

/// <summary>
/// v0.103.2's ARM64 MonoMod backend can segfault when dozens of native
/// trampolines are emitted in one uninterrupted initializer burst. Installing
/// one Harmony class at a time on the Godot main thread, with a short frame
/// interval, avoids racing the runtime's method preparation and instruction
/// cache maintenance while retaining the complete gameplay patch set.
/// </summary>
internal partial class AndroidPatchInstaller : Node
{
    private const double InitialDelaySeconds = 0.75;
    private const double PatchIntervalSeconds = 0.25;

    private readonly Queue<Type> _pending = new();
    private Harmony? _harmony;
    private double _remaining = InitialDelaySeconds;
    private int _total;
    private int _completed;

    internal void Configure(Harmony harmony, IReadOnlyList<Type> patchTypes)
    {
        _harmony = harmony;
        foreach (var type in patchTypes)
            _pending.Enqueue(type);
        _total = _pending.Count;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        SetProcess(true);
        MainFile.Logger.Info($"[BetterDefect] Android patch queue attached; {_total} classes pending.");
    }

    public override void _Process(double delta)
    {
        _remaining -= delta;
        if (_remaining > 0)
            return;

        _remaining = PatchIntervalSeconds;
        if (_harmony is null || _pending.Count == 0)
        {
            Finish();
            return;
        }

        var type = _pending.Dequeue();
        MainFile.PatchOne(_harmony, type);
        _completed++;

        if (_pending.Count == 0)
            Finish();
    }

    private void Finish()
    {
        SetProcess(false);
        MainFile.Logger.Info($"[BetterDefect] Android patch queue complete: {_completed}/{_total} classes installed.");
        // ModelDb.Init can finish while Android is still installing Harmony
        // classes.  In that case its postfix was not present when the
        // canonical cards and Defect pool were created. Rebuild the cached
        // vanilla 88-card pool only after GenerateAllCards is patched.
        OldDefectCards.RefreshAfterDeferredPatchInstall();
        // LocManager.Initialize has also already finished by the time Android's
        // delayed Harmony queue installs our localization lifecycle patches.
        // Merge the restored cards' titles/descriptions explicitly here;
        // otherwise encyclopedia sorting throws on BD_*.title and the injected
        // cards appear to be missing even though the Defect pool contains 114.
        BdLocalization.MergeIntoLocManager();
        BdCardVersionUpgrades.RefreshAllCanonicalModels();
        BdCardVersionUpgrades.ReapplyPersistedTransformationsToLoadedCards("Android patch queue completion");
        QueueFree();
    }
}




