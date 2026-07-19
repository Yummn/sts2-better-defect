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
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type == typeof(BdLocExistsPatch) || type == typeof(BdLocRawPatch))
            {
                Logger.Warn($"[BetterDefect] skipping obsolete {type.FullName}; localization is merged through LocManager instead of detouring LocString.");
                continue;
            }
            Logger.Info($"[BetterDefect] patching {type.FullName}");
            harmony.CreateClassProcessor(type).Patch();
            Logger.Info($"[BetterDefect] patched {type.FullName}");
        }
        BdDynamicOdds.InitializeStorage();
        BdLocalization.MergeIntoLocManager();
        BdDynamicOddsStatsHud.EnsureInstalled();
        Logger.Info("[BetterDefect] loaded v0.8.4: card-point HUD is bound to the exact visible NCardLibrary and follows submenu visibility changes; Android skips the unsafe NCard.Model setter detour; BaseLib not required.");
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
}



