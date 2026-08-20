using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;
using StackCard = MegaCrit.Sts2.Core.Models.Cards.Stack;

namespace CodexRecycleStackV01162Test;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal static MegaCrit.Sts2.Core.Logging.Logger Log { get; } = new("CodexRecycleStackV01162Test", LogType.Generic);
    public static void Initialize()
    {
        new Harmony("CodexRecycleStackV01162Test").PatchAll();
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new Runner());
        Log.Info("[CodexRecycleStackV01162Test] runner installed.");
    }
}

[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand),
    [typeof(PlayerChoiceContext), typeof(Player), typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>), typeof(AbstractModel)])]
internal static class SelectionPatch
{
    internal static CardModel? Victim { get; set; }
    internal static int SelectedCost { get; private set; }
    private static bool Prefix(AbstractModel source, ref Task<IEnumerable<CardModel>> __result)
    {
        if (Victim == null || source.Id.Entry is not ("BD_RECYCLE" or "STACK")) return true;
        SelectedCost = Victim.EnergyCost.CostsX
            ? Math.Max(0, Victim.Owner.PlayerCombatState.Energy)
            : Math.Max(0, Victim.EnergyCost.GetResolved());
        __result = Task.FromResult<IEnumerable<CardModel>>([Victim]);
        MainFile.Log.Info($"[CodexRecycleStackV01162Test] auto-selected {Victim.Id.Entry} for {source.Id.Entry}; selectedCost={SelectedCost}");
        return false;
    }
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.GainEnergy))]
internal static class EnergyGainPatch
{
    internal static List<decimal> Amounts { get; } = [];
    internal static void Reset() => Amounts.Clear();
    private static void Prefix(decimal amount) => Amounts.Add(amount);
}

public partial class Runner : Node
{
    public override void _Ready() => _ = RunAsync();
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static async Task WithTimeout(Task task, string label)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20))) != task) throw new TimeoutException(label);
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

            var player = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState()) ?? throw new InvalidOperationException("player unavailable");
            var recycleCanonical = Enable("BD_RECYCLE");
            var stackCanonical = Enable("STACK");
            Require(recycleCanonical.Rarity == CardRarity.Common, $"Recycle rarity={recycleCanonical.Rarity}");
            Require(stackCanonical.Rarity == CardRarity.Common, $"Stack rarity={stackCanonical.Rarity}");
            Require(!recycleCanonical.Keywords.Contains(CardKeyword.Exhaust), "Recycle canonical still Exhausts");
            Require(stackCanonical.Keywords.Contains(CardKeyword.Exhaust), "Stack canonical does not Exhaust");
            Require(new LocString("cards", "BD_RECYCLE.description").GetRawText().Contains("当前费用"), "Recycle text stale");
            Require(new LocString("cards", "STACK.description").GetRawText().Contains("充能球栏位"), "Stack text stale");

            var context = new BlockingPlayerChoiceContext();
            var console = new DevConsole(true);
            await Add(console, "COLD_SNAP");
            var recycleVictim = PileType.Hand.GetPile(player).Cards.Last(c => c.Id.Entry == "COLD_SNAP");
            await Add(console, "BD_RECYCLE");
            var recycle = PileType.Hand.GetPile(player).Cards.Last(c => c.Id.Entry == "BD_RECYCLE");
            SelectionPatch.Victim = recycleVictim;
            var energyBefore = player.PlayerCombatState.Energy;
            EnergyGainPatch.Reset();
            await WithTimeout(CardCmd.AutoPlay(context, recycle, null), "Recycle play timed out");
            var refunded = SelectionPatch.SelectedCost;
            var exhaust = PileType.Exhaust.GetPile(player).Cards;
            Require(exhaust.Contains(recycleVictim), "Recycle did not exhaust selected card");
            Require(!exhaust.Contains(recycle), "Recycle exhausted itself");
            Require(EnergyGainPatch.Amounts.Contains(refunded),
                $"Recycle did not request the {refunded}-energy refund; gains=[{string.Join(",", EnergyGainPatch.Amounts)}]");

            await Add(console, "ZAP");
            var stackVictim = PileType.Hand.GetPile(player).Cards.Last(c => c.Id.Entry == "ZAP");
            await Add(console, "STACK");
            var stack = PileType.Hand.GetPile(player).Cards.OfType<StackCard>().Last();
            SelectionPatch.Victim = stackVictim;
            var capacityBefore = player.PlayerCombatState.OrbQueue.Capacity;
            await WithTimeout(CardCmd.AutoPlay(context, stack, null), "Stack play timed out");
            Require(PileType.Exhaust.GetPile(player).Cards.Contains(stackVictim), "Stack did not exhaust selected card");
            Require(PileType.Exhaust.GetPile(player).Cards.Contains(stack), "Stack did not exhaust itself");
            Require(player.PlayerCombatState.OrbQueue.Capacity == capacityBefore + 1, "Stack did not add one Orb slot");

            await Add(console, "BD_RECYCLE");
            var recyclePlus = PileType.Hand.GetPile(player).Cards.Last(c => c.Id.Entry == "BD_RECYCLE");
            recyclePlus.UpgradeInternal(); recyclePlus.FinalizeUpgradeInternal();
            await Add(console, "STACK");
            var stackPlus = PileType.Hand.GetPile(player).Cards.OfType<StackCard>().Last();
            stackPlus.UpgradeInternal(); stackPlus.FinalizeUpgradeInternal();
            Require(recyclePlus.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0 && !recyclePlus.Keywords.Contains(CardKeyword.Exhaust), "Recycle+ metadata mismatch");
            Require(stackPlus.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0 && stackPlus.Keywords.Contains(CardKeyword.Exhaust), "Stack+ metadata mismatch");

            MainFile.Log.Info($"[CodexRecycleStackV01162Test] OBSERVE recycleRefund={refunded} energy={energyBefore}->{player.PlayerCombatState.Energy} energyGains=[{string.Join(",", EnergyGainPatch.Amounts)}] stackSlots={capacityBefore}->{player.PlayerCombatState.OrbQueue.Capacity}");
            MainFile.Log.Info("[CodexRecycleStackV01162Test] PASS: Recycle refunds selected cost without self-Exhaust; Stack inherits the old orb-slot effect and self-Exhausts; both upgrades cost 0.");
        }
        catch (Exception ex) { MainFile.Log.Error($"[CodexRecycleStackV01162Test] FAIL: {ex}"); }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task Add(DevConsole console, string id)
    {
        var result = console.ProcessCommand($"card {id}");
        if (!result.success) throw new InvalidOperationException(result.msg);
        if (result.task != null) await result.task;
        await Task.Delay(120);
    }
    private static CardModel Enable(string id)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BetterDefect");
        var state = assembly.GetType("BetterDefect.BdCardUpgradeState", true)!;
        var versions = assembly.GetType("BetterDefect.BdCardVersionUpgrades", true)!;
        var isEnabled = state.GetMethod("IsCardVersionUpgraded", BindingFlags.Public | BindingFlags.Static)!;
        var toggle = state.GetMethod("ToggleCardVersionUpgrade", BindingFlags.Public | BindingFlags.Static)!;
        var card = ModelDb.AllCards.First(c => c.Id.Entry == id);
        if (!(bool)(isEnabled.Invoke(null, [card]) ?? false))
        {
            while (!(bool)(toggle.Invoke(null, [card]) ?? false))
            {
                var donor = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry != id && (bool)(isEnabled.Invoke(null, [c]) ?? false));
                if (donor == null || !(bool)(toggle.Invoke(null, [donor]) ?? false)) throw new InvalidOperationException($"cannot enable {id}");
            }
        }
        versions.GetMethod("RefreshCanonicalFor", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [card]);
        assembly.GetType("BetterDefect.BdLocalization", true)!.GetMethod("RefreshVersionSensitiveCardDescriptions", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
        return card;
    }
}
