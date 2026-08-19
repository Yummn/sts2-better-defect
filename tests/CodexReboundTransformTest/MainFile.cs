using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using System.Reflection;

namespace CodexReboundTransformTest;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal static MegaCrit.Sts2.Core.Logging.Logger Log { get; } = new("CodexReboundTransformTest", LogType.Generic);
    public static void Initialize()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new Runner());
        Log.Info("[CodexReboundTransformTest] runner installed.");
    }
}

internal sealed class FirstSelector : ICardSelector
{
    private readonly CardModel _desired;
    public FirstSelector(CardModel desired) => _desired = desired;
    public CardModel? Selected { get; private set; }
    public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
    {
        var list = options.ToList();
        MainFile.Log.Info($"[CodexReboundTransformTest] selector called: options={list.Count}, desiredPile={_desired.Pile?.Type}, ids={string.Join(",", list.Select(c => c.Id.Entry))}.");
        Selected = list.FirstOrDefault(card => ReferenceEquals(card, _desired));
        return Task.FromResult(Selected is null ? Enumerable.Empty<CardModel>() : new[] { Selected });
    }
    public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) => default;
}

public partial class Runner : Node
{
    private static int _started;
    private bool _changedSetting;
    public override void _Ready() => _ = RunAsync();
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static async Task Timeout(Task task, string label)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))) != task)
            throw new TimeoutException(label);
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
            var target = player.Creature.CombatState.HittableEnemies.First(e => e.IsAlive && e.IsHittable);
            target.LoseBlockInternal(target.Block);
            _changedSetting = Enable();

            var console = new DevConsole(true);
            if (!console.ProcessCommand("card REBOUND").success) throw new InvalidOperationException("could not create Rebound");
            if (!console.ProcessCommand("card STRIKE_DEFECT").success) throw new InvalidOperationException("could not create fixture card");
            await Wait(.2);
            var hand = PileType.Hand.GetPile(player).Cards;
            var rebound = hand.Last(c => c is Rebound);
            var fixture = hand.Last(c => c is StrikeDefect);
            await CardPileCmd.Add(fixture, PileType.Discard, CardPilePosition.Top);

            var cost = rebound.EnergyCost.GetWithModifiers(CostModifiers.None);
            var damage = rebound.DynamicVars.Damage.IntValue;
            if (cost != 1 || damage != 9) throw new InvalidOperationException($"values cost={cost}, damage={damage}");
            player.PlayerCombatState.Energy = 9;
            var hpBefore = target.CurrentHp;
            var selector = new FirstSelector(fixture);
            using (CardSelectCmd.UseSelector(selector))
                await Timeout(CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), rebound, target), "Rebound play timed out");
            var hpDelta = hpBefore - target.CurrentHp;
            var drawn = (await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), 1, player)).FirstOrDefault();
            if (!ReferenceEquals(drawn, fixture)) throw new InvalidOperationException("selected card was not on draw-pile top");
            if (hpDelta != 9) throw new InvalidOperationException($"damage delta={hpDelta}, expected 9");
            MainFile.Log.Info("[CodexReboundTransformTest] PASS: dealt 9 damage, selected a discard card, placed it on draw-pile top, and completed play.");
        }
        catch (Exception ex) { MainFile.Log.Error($"[CodexReboundTransformTest] FAIL: {ex}"); }
        finally
        {
            if (_changedSetting) Disable();
            await Wait(.5);
            GetTree().Quit();
        }
    }

    private static bool Enable() => Set(true);
    private static void Disable() => Set(false);
    private static bool Set(bool desired)
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BetterDefect");
        var state = asm.GetType("BetterDefect.BdCardUpgradeState", true)!;
        var versions = asm.GetType("BetterDefect.BdCardVersionUpgrades", true)!;
        var canonical = ModelDb.Card<Rebound>();
        var isEnabled = state.GetMethod("IsCardVersionUpgraded", BindingFlags.Public | BindingFlags.Static)!;
        var toggle = state.GetMethod("ToggleCardVersionUpgrade", BindingFlags.Public | BindingFlags.Static)!;
        var refresh = versions.GetMethod("RefreshCanonicalFor", BindingFlags.NonPublic | BindingFlags.Static)!;
        var old = (bool)(isEnabled.Invoke(null, new object[] { canonical }) ?? false);
        if (old != desired)
        {
            if (!(bool)(toggle.Invoke(null, new object[] { canonical }) ?? false)) throw new InvalidOperationException("toggle rejected");
            refresh.Invoke(null, new object[] { canonical });
        }
        return !old && desired;
    }
}
