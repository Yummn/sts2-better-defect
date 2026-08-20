using System.Collections;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace CodexHyperbeamV111Test;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal static MegaCrit.Sts2.Core.Logging.Logger Log { get; } = new("CodexHyperbeamV111Test", LogType.Generic);
    public static void Initialize()
    {
        new Harmony("CodexHyperbeamV111Test").PatchAll();
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new Runner());
    }
}

public partial class Runner : Node
{
    private IList? _keys;
    private List<string>? _savedKeys;
    public override void _Ready() => _ = RunAsync();
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static async Task Timeout(Task task, string label)
    {
        if (await Task.WhenAny(task, Task.Delay(20000)) != task) throw new TimeoutException(label);
        await task;
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
            SetHyperbeamTransformed(true);
            var result = new DevConsole(true).ProcessCommand("card HYPERBEAM");
            if (!result.success) throw new InvalidOperationException(result.msg);
            await Task.Delay(150);

            var card = PileType.Hand.GetPile(player).Cards.Last(c => c is Hyperbeam);
            if (card.DynamicVars.Damage.BaseValue != 24m)
                throw new InvalidOperationException($"base damage expected 24, actual {card.DynamicVars.Damage.BaseValue}");
            if (card.DynamicVars["FocusPower"].BaseValue != 3m)
                throw new InvalidOperationException($"Focus loss expected 3, actual {card.DynamicVars["FocusPower"].BaseValue}");

            var before = FocusAmount(player.Creature.Powers);
            HyperbeamVfxProbe.BeamCreates = 0;
            HyperbeamVfxProbe.ImpactCreates = 0;
            var context = new BlockingPlayerChoiceContext();
            await Timeout(CardCmd.AutoPlay(context, card, null), "Hyperbeam play timeout");

            if (HyperbeamVfxProbe.BeamCreates < 1)
                throw new InvalidOperationException("native NHyperbeamVfx was not created");
            if (HyperbeamVfxProbe.ImpactCreates < 1)
                throw new InvalidOperationException("native NHyperbeamImpactVfx was not created");

            var temporary = player.Creature.Powers.FirstOrDefault(p => p.GetType().Name == "BdHyperbeamTemporaryFocusDownPower")
                ?? throw new InvalidOperationException("temporary Focus-down power missing after play");
            var during = FocusAmount(player.Creature.Powers);
            if (temporary.Amount != 3m || during != before - 3m)
                throw new InvalidOperationException($"temporary loss mismatch: before={before}, during={during}, power={temporary.Amount}");

            await temporary.AfterSideTurnEnd(context, CombatSide.Player, [player.Creature]);
            var after = FocusAmount(player.Creature.Powers);
            if (after != before || player.Creature.Powers.Contains(temporary))
                throw new InvalidOperationException($"Focus restore mismatch: before={before}, after={after}, temporaryStillPresent={player.Creature.Powers.Contains(temporary)}");

            MainFile.Log.Info($"[CodexHyperbeamV111Test] PASS: native beam={HyperbeamVfxProbe.BeamCreates}, impacts={HyperbeamVfxProbe.ImpactCreates}; transformed Hyperbeam applied -3 temporary Focus ({before}->{during}) and restored it at turn end ({during}->{after}).");
        }
        catch (Exception ex)
        {
            MainFile.Log.Error($"[CodexHyperbeamV111Test] FAIL: {ex}");
        }
        finally
        {
            RestoreKeys();
        }
    }

    private static decimal FocusAmount(IEnumerable<PowerModel> powers) =>
        powers.FirstOrDefault(p => p is FocusPower)?.Amount ?? 0m;

    private void SetHyperbeamTransformed(bool enabled)
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BetterDefect");
        var type = asm.GetType("BetterDefect.BdCardUpgradeState", true)!;
        var state = AccessTools.Field(type, "_state")!.GetValue(null)!;
        _keys = (IList)AccessTools.Property(state.GetType(), "UpgradedCards")!.GetValue(state)!;
        _savedKeys = _keys.Cast<string>().ToList();
        var card = ModelDb.Card<Hyperbeam>();
        var key = (string)AccessTools.Method(type, "CardKey")!.Invoke(null, [card])!;
        for (var i = _keys.Count - 1; i >= 0; i--)
            if (string.Equals(_keys[i] as string, key, StringComparison.Ordinal)) _keys.RemoveAt(i);
        if (enabled) _keys.Add(key);
        if (!(bool)AccessTools.Method(type, "IsCardVersionUpgraded")!.Invoke(null, [card])!)
            throw new InvalidOperationException("could not enable transformed Hyperbeam");
    }

    private void RestoreKeys()
    {
        if (_keys == null || _savedKeys == null) return;
        _keys.Clear();
        foreach (var key in _savedKeys) _keys.Add(key);
    }
}

internal static class HyperbeamVfxProbe
{
    internal static int BeamCreates;
    internal static int ImpactCreates;
}

[HarmonyPatch(typeof(NHyperbeamVfx), nameof(NHyperbeamVfx.Create), typeof(Creature), typeof(Creature))]
internal static class HyperbeamBeamCreatePatch
{
    private static void Postfix() => HyperbeamVfxProbe.BeamCreates++;
}

[HarmonyPatch(typeof(NHyperbeamImpactVfx), nameof(NHyperbeamImpactVfx.Create), typeof(Creature), typeof(Creature))]
internal static class HyperbeamImpactCreatePatch
{
    private static void Postfix() => HyperbeamVfxProbe.ImpactCreates++;
}
