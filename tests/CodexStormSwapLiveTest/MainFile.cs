using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace CodexStormSwapLiveTest;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal static MegaCrit.Sts2.Core.Logging.Logger Log { get; } = new("CodexStormSwapLiveTest", LogType.Generic);
    public static void Initialize()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new Runner());
    }
}

public partial class Runner : Node
{
    public override void _Ready() => _ = RunAsync();
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static async Task Timeout(Task task, string label)
    {
        if (await Task.WhenAny(task, Task.Delay(20000)) != task) throw new TimeoutException(label);
        await task;
    }

    private static void EnsureTransformed(CardModel canonical)
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BetterDefect");
        var state = asm.GetType("BetterDefect.BdCardUpgradeState", true)!;
        var versions = asm.GetType("BetterDefect.BdCardVersionUpgrades", true)!;
        var isEnabled = (bool)state.GetMethod("IsCardVersionUpgraded", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [canonical])!;
        if (!isEnabled)
            state.GetMethod("ToggleCardVersionUpgrade", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [canonical]);
        versions.GetMethod("RefreshCanonicalFor", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [canonical]);
    }

    private async Task RunAsync()
    {
        try
        {
            for (var i = 0; i < 480 && !RunManager.Instance.IsInProgress; i++) await Wait(.25);
            for (var i = 0; i < 2400 && !CombatManager.Instance.IsInProgress; i++) await Wait(.25);
            if (!CombatManager.Instance.IsInProgress) throw new InvalidOperationException("combat unavailable");
            await Wait(2);

            var player = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState())!;
            var context = new BlockingPlayerChoiceContext();
            var console = new DevConsole(true);
            var stormCanonical = ModelDb.AllCards.First(c => c.Id.Entry == "STORM");
            var staticCanonical = ModelDb.AllCards.First(c => c.Id.Entry == "BD_STATIC_DISCHARGE");
            EnsureTransformed(stormCanonical);
            EnsureTransformed(staticCanonical);

            var addStorm = console.ProcessCommand("card STORM");
            if (!addStorm.success) throw new InvalidOperationException(addStorm.msg);
            await Task.Delay(150);
            var storm = PileType.Hand.GetPile(player).Cards.Last(c => c.Id.Entry == "STORM");
            if (storm.EnergyCost.GetWithModifiers(CostModifiers.All) != 1 || storm.DynamicVars["StormPower"].BaseValue != 2m || !storm.Keywords.Contains(CardKeyword.Innate))
                throw new InvalidOperationException($"Storm metadata mismatch: cost={storm.EnergyCost.GetWithModifiers(CostModifiers.All)} amount={storm.DynamicVars["StormPower"].BaseValue} innate={storm.Keywords.Contains(CardKeyword.Innate)}");
            await Timeout(CardCmd.AutoPlay(context, storm, null), "Storm play timeout");
            if (player.Creature.Powers.All(p => p.GetType().Name != "BdStormChargePower")) throw new InvalidOperationException("BdStormChargePower missing");
            if (player.Creature.Powers.Any(p => p.GetType().Name == "StormPower")) throw new InvalidOperationException("native StormPower incorrectly remained");

            await OrbCmd.Channel<LightningOrb>(context, player);
            await OrbCmd.Channel<LightningOrb>(context, player);
            var charge = player.Creature.Powers.FirstOrDefault(p => p.GetType().Name == "BdStaticDischargeChargePower") ?? throw new InvalidOperationException("charge missing");
            if (charge.Amount != 4m) throw new InvalidOperationException($"charge expected 4, actual {charge.Amount}");

            var addAttack = console.ProcessCommand("card GUNK_UP");
            if (!addAttack.success) throw new InvalidOperationException(addAttack.msg);
            await Task.Delay(150);
            var attack = PileType.Hand.GetPile(player).Cards.Last(c => c.Id.Entry == "GUNK_UP");
            var target = player.Creature.CombatState.HittableEnemies
                .Where(e => e.IsAlive && e.IsHittable)
                .OrderByDescending(e => e.CurrentHp)
                .First();
            var history = CombatManager.Instance.History;
            var before = history.Entries.OfType<DamageReceivedEntry>().Count();
            await Timeout(CardCmd.AutoPlay(context, attack, target), "Gunk Up play timeout");
            var hits = history.Entries.OfType<DamageReceivedEntry>().Skip(before).Where(e => ReferenceEquals(e.CardSource, attack)).ToList();
            var remaining = player.Creature.Powers.FirstOrDefault(p => p.GetType().Name == "BdStaticDischargeChargePower");
            if (hits.Count != 3 || hits.Any(h => h.Result.TotalDamage < 8m) || remaining != null)
                throw new InvalidOperationException($"multi-hit mismatch hits={hits.Count}, total=[{string.Join(",", hits.Select(h => h.Result.TotalDamage))}], unblocked=[{string.Join(",", hits.Select(h => h.Result.UnblockedDamage))}], remaining={remaining?.Amount}");

            var addStatic = console.ProcessCommand("card BD_STATIC_DISCHARGE");
            if (!addStatic.success) throw new InvalidOperationException(addStatic.msg);
            await Task.Delay(150);
            var staticCard = PileType.Hand.GetPile(player).Cards.Last(c => c.Id.Entry == "BD_STATIC_DISCHARGE");
            staticCard.UpgradeInternal();
            staticCard.FinalizeUpgradeInternal();
            if (staticCard.EnergyCost.GetWithModifiers(CostModifiers.All) != 1 || staticCard.DynamicVars["Amount"].BaseValue != 2m || !staticCard.Keywords.Contains(CardKeyword.Innate))
                throw new InvalidOperationException($"Static+ metadata mismatch cost={staticCard.EnergyCost.GetWithModifiers(CostModifiers.All)} amount={staticCard.DynamicVars["Amount"].BaseValue} innate={staticCard.Keywords.Contains(CardKeyword.Innate)}");
            await Timeout(CardCmd.AutoPlay(context, staticCard, null), "Static play timeout");
            var staticPower = player.Creature.Powers.FirstOrDefault(p => p.GetType().Name == "BdStaticDischargePower");
            if (staticPower == null || staticPower.Amount != 2)
                throw new InvalidOperationException("transformed Static Discharge power missing or wrong amount");

            MainFile.Log.Info("[CodexStormSwapLiveTest] PASS: Storm custom power, +4 on all three hits with one consumption; Static+ is 1-cost, Innate and Amount=2.");
        }
        catch (Exception ex) { MainFile.Log.Error($"[CodexStormSwapLiveTest] FAIL: {ex}"); }
    }
}
