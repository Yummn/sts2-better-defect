using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace CodexBulkUpV01161Test;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal static MegaCrit.Sts2.Core.Logging.Logger Log { get; } = new("CodexBulkUpV01161Test", LogType.Generic);
    public static void Initialize()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new Runner());
        Log.Info("[CodexBulkUpV01161Test] runner installed.");
    }
}

public partial class Runner : Node
{
    public override void _Ready() => _ = RunAsync();
    private async Task Wait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static async Task WithTimeout(Task task, string label)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20))) != task)
            throw new TimeoutException($"{label} timed out");
        await task;
    }

    private async Task RunAsync()
    {
        try
        {
            for (var i = 0; i < 480 && !RunManager.Instance.IsInProgress; i++) await Wait(0.25);
            for (var i = 0; i < 2400 && !CombatManager.Instance.IsInProgress; i++) await Wait(0.25);
            if (!CombatManager.Instance.IsInProgress) throw new InvalidOperationException("combat unavailable");
            await Wait(2);

            var player = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState())
                ?? throw new InvalidOperationException("local player unavailable");
            EnsureTransformationEnabled();

            var text = new LocString("cards", "BULK_UP.description").GetRawText();
            if (!text.Contains("当前充能球栏位数量"))
                throw new InvalidOperationException($"unexpected transformed description: {text}");

            var context = new BlockingPlayerChoiceContext();
            var console = new DevConsole(true);
            var add = console.ProcessCommand("card BULK_UP");
            if (!add.success) throw new InvalidOperationException(add.msg);
            await Task.Delay(150);
            var card = PileType.Hand.GetPile(player).Cards.OfType<BulkUp>().Last();
            var baseCost = card.EnergyCost.GetWithModifiers(CostModifiers.None);
            if (baseCost != 2) throw new InvalidOperationException($"base cost={baseCost}, expected 2");

            var beforeSlots = player.PlayerCombatState.OrbQueue.Capacity;
            var beforeStrength = player.Creature.GetPower<StrengthPower>()?.Amount ?? 0m;
            var beforeDexterity = player.Creature.GetPower<DexterityPower>()?.Amount ?? 0m;
            await WithTimeout(CardCmd.AutoPlay(context, card, null), "playing transformed Bulk Up");
            var afterSlots = player.PlayerCombatState.OrbQueue.Capacity;
            var strengthGain = (player.Creature.GetPower<StrengthPower>()?.Amount ?? 0m) - beforeStrength;
            var dexterityGain = (player.Creature.GetPower<DexterityPower>()?.Amount ?? 0m) - beforeDexterity;
            var expectedSlots = Math.Max(0, beforeSlots - 1);

            var betterDefect = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BetterDefect");
            var legacyPower = betterDefect.GetType("BetterDefect.Cards.BdBulkUpPower", true)!;
            var hasLegacyPower = player.Creature.Powers.Any(p => p.GetType() == legacyPower);
            MainFile.Log.Info($"[CodexBulkUpV01161Test] OBSERVE beforeSlots={beforeSlots} afterSlots={afterSlots} strengthGain={strengthGain} dexterityGain={dexterityGain} legacyPower={hasLegacyPower} text={text}");
            if (afterSlots != expectedSlots || strengthGain != afterSlots || dexterityGain != afterSlots || hasLegacyPower)
                throw new InvalidOperationException("runtime effect mismatch");

            add = console.ProcessCommand("card BULK_UP");
            if (!add.success) throw new InvalidOperationException(add.msg);
            await Task.Delay(150);
            var upgraded = PileType.Hand.GetPile(player).Cards.OfType<BulkUp>().Last();
            upgraded.UpgradeInternal();
            upgraded.FinalizeUpgradeInternal();
            var upgradedCost = upgraded.EnergyCost.GetWithModifiers(CostModifiers.None);
            if (upgradedCost != 1) throw new InvalidOperationException($"upgraded cost={upgradedCost}, expected 1");

            MainFile.Log.Info($"[CodexBulkUpV01161Test] PASS: lost one slot, then gained {afterSlots} Strength and Dexterity; upgraded cost is 1.");
        }
        catch (Exception ex) { MainFile.Log.Error($"[CodexBulkUpV01161Test] FAIL: {ex}"); }
    }

    private static void EnsureTransformationEnabled()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BetterDefect");
        var state = assembly.GetType("BetterDefect.BdCardUpgradeState", true)!;
        var card = ModelDb.Card<BulkUp>();
        var isEnabled = state.GetMethod("IsCardVersionUpgraded", BindingFlags.Public | BindingFlags.Static)!;
        var toggle = state.GetMethod("ToggleCardVersionUpgrade", BindingFlags.Public | BindingFlags.Static)!;
        if (!(bool)(isEnabled.Invoke(null, [card]) ?? false) && !(bool)(toggle.Invoke(null, [card]) ?? false))
            throw new InvalidOperationException("unable to enable Bulk Up transformation");
        var versions = assembly.GetType("BetterDefect.BdCardVersionUpgrades", true)!;
        versions.GetMethod("RefreshCanonicalFor", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [card]);
        assembly.GetType("BetterDefect.BdLocalization", true)!
            .GetMethod("RefreshVersionSensitiveCardDescriptions", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    }
}
