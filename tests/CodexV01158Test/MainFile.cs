using System.Collections;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CodexV01158Test;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal static MegaCrit.Sts2.Core.Logging.Logger Log { get; } = new("CodexV01158Test", LogType.Generic);
    public static void Initialize()
    {
        if (Engine.GetMainLoop() is SceneTree tree) tree.Root.CallDeferred(Node.MethodName.AddChild, new Runner());
        Log.Info("[CodexV01158Test] runner installed.");
    }
}

public partial class Runner : Node
{
    private static int _started;
    public override void _Ready() => _ = RunAsync();
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static async Task Timeout(Task task, string label)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))) != task) throw new TimeoutException(label);
        await task;
    }

    private async Task RunAsync()
    {
        try
        {
            for (var i = 0; i < 480 && !CombatManager.Instance.IsInProgress; i++) await Wait(.25);
            if (!CombatManager.Instance.IsInProgress || Interlocked.Exchange(ref _started, 1) != 0) return;
            await Wait(2);
            var player = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState()) ?? throw new InvalidOperationException("player unavailable");
            var console = new DevConsole(true);

            SetTransformed("BD_STEAM_BARRIER", false);
            var steamBase = await Create(console, player, "BD_STEAM_BARRIER");
            AssertCard(steamBase, 0, 6, false, "vanilla Steam Barrier");
            steamBase.UpgradeInternal(); steamBase.FinalizeUpgradeInternal();
            AssertCard(steamBase, 0, 8, false, "vanilla Steam Barrier+");

            SetTransformed("BD_STEAM_BARRIER", true);
            var steam = await Create(console, player, "BD_STEAM_BARRIER");
            AssertCard(steam, 0, 8, false, "transformed Steam Barrier");
            player.Creature.LoseBlockInternal(player.Creature.Block);
            await Timeout(CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), steam, null), "Steam Barrier play timed out");
            if (player.Creature.Block != 8 || steam.DynamicVars.Block.IntValue != 7)
                throw new InvalidOperationException($"Steam Barrier play mismatch: block={player.Creature.Block}, next={steam.DynamicVars.Block.IntValue}");
            var steamPlus = await Create(console, player, "BD_STEAM_BARRIER");
            steamPlus.UpgradeInternal(); steamPlus.FinalizeUpgradeInternal();
            AssertCard(steamPlus, 0, 12, false, "transformed Steam Barrier+");

            SetTransformed("BD_RECYCLE", true);
            foreach (var card in PileType.Hand.GetPile(player).Cards.ToList()) await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Top);
            var recycle = await Create(console, player, "BD_RECYCLE");
            AssertCard(recycle, 1, null, true, "transformed Recycle");
            var queue = player.PlayerCombatState.OrbQueue;
            queue.Clear();
            if (queue.Capacity < 3) queue.AddCapacity(3 - queue.Capacity);
            var before = queue.Capacity;
            player.PlayerCombatState.Energy = 9;
            await Timeout(CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), recycle, null), "empty-hand Recycle timed out");
            if (queue.Capacity != before + 1 || !PileType.Exhaust.GetPile(player).Cards.Contains(recycle))
                throw new InvalidOperationException($"empty-hand Recycle mismatch: capacity={before}->{queue.Capacity}");

            var recyclePlus = await Create(console, player, "BD_RECYCLE");
            recyclePlus.UpgradeInternal(); recyclePlus.FinalizeUpgradeInternal();
            AssertCard(recyclePlus, 0, null, true, "transformed Recycle+");
            MainFile.Log.Info("[CodexV01158Test] PASS: Steam Barrier is 6(8) normally and 8(12) only when transformed; transformed Recycle is 1(0), Exhaust, and gained an Orb slot with no exhaustable hand card.");
        }
        catch (Exception ex) { MainFile.Log.Error($"[CodexV01158Test] FAIL: {ex}"); }
        finally { await Wait(.5); GetTree().Quit(); }
    }

    private static async Task<CardModel> Create(DevConsole console, MegaCrit.Sts2.Core.Entities.Players.Player player, string id)
    {
        var result = console.ProcessCommand($"card {id}");
        if (!result.success) throw new InvalidOperationException($"card {id} failed: {result.msg}");
        if (result.task is not null) await result.task;
        await Task.Delay(100);
        return PileType.Hand.GetPile(player).Cards.Last(card => card.Id.Entry == id);
    }

    private static void AssertCard(CardModel card, int cost, int? block, bool exhaust, string label)
    {
        var actualCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
        var actualExhaust = card.Keywords.Contains(CardKeyword.Exhaust);
        var actualBlock = block.HasValue ? card.DynamicVars.Block.IntValue : 0;
        if (actualCost != cost || actualExhaust != exhaust || (block.HasValue && actualBlock != block.Value))
            throw new InvalidOperationException($"{label}: cost={actualCost}, block={actualBlock}, exhaust={actualExhaust}");
    }

    private static void SetTransformed(string id, bool enabled)
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BetterDefect");
        var stateType = asm.GetType("BetterDefect.BdCardUpgradeState", true)!;
        var versions = asm.GetType("BetterDefect.BdCardVersionUpgrades", true)!;
        var state = stateType.GetField("_state", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var keys = (IList)state.GetType().GetProperty("UpgradedCards")!.GetValue(state)!;
        var card = ModelDb.AllCards.First(c => c.Id.Entry == id);
        var key = (string)stateType.GetMethod("CardKey", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { card })!;
        for (var i = keys.Count - 1; i >= 0; i--) if (string.Equals(keys[i] as string, key, StringComparison.Ordinal)) keys.RemoveAt(i);
        if (enabled) keys.Add(key);
        versions.GetMethod("RefreshCanonicalFor", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { card });
    }
}
