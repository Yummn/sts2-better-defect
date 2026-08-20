using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
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
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CodexHailstormLiveTest;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal static MegaCrit.Sts2.Core.Logging.Logger Log { get; } =
        new("CodexHailstormLiveTest", LogType.Generic);

    public static void Initialize()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new LiveTestRunner());
        Log.Info("[CodexHailstormLiveTest] root combat test runner installed.");
    }
}

[HarmonyPatch(typeof(NCombatRoom), "_Ready")]
internal static class CombatRoomReadyPatch
{
    private static void Postfix(NCombatRoom __instance)
    {
        __instance.CallDeferred(Node.MethodName.AddChild, new LiveTestRunner());
        MainFile.Log.Info("[CodexHailstormLiveTest] runner scheduled for combat room.");
    }
}

public partial class LiveTestRunner : Node
{
    public override void _Ready() => _ = RunAsync();

    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static async Task WithTimeout(Task task, string label)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20))) != task)
            throw new TimeoutException($"{label} exceeded 20 seconds");
        await task;
    }

    private async Task RunAsync()
    {
        try
        {
            for (var i = 0; i < 480 && !RunManager.Instance.IsInProgress; i++)
                await WaitSeconds(0.25);
            if (!RunManager.Instance.IsInProgress)
                throw new InvalidOperationException("no active run after 120 seconds");

            for (var i = 0; i < 2400 && !CombatManager.Instance.IsInProgress; i++)
                await WaitSeconds(0.25);
            if (!CombatManager.Instance.IsInProgress)
                throw new InvalidOperationException("combat did not start after 600 seconds");

            await WaitSeconds(2);
            var player = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState())
                ?? throw new InvalidOperationException("local player unavailable");
            var context = new BlockingPlayerChoiceContext();
            var history = CombatManager.Instance.History;

            EnsureHailstormTransformationEnabled();
            var cardRaw = new LocString("cards", "HAILSTORM.description").GetRawText();
            var powerRaw = new LocString("powers", "HAILSTORM_POWER.smartDescription").GetRawText();
            MainFile.Log.Info($"[CodexHailstormLiveTest] CARD_TEXT={cardRaw}");
            MainFile.Log.Info($"[CodexHailstormLiveTest] POWER_TEXT={powerRaw}");
            if (!cardRaw.Contains("每有1个") || !cardRaw.Contains("分别对所有敌人"))
                throw new InvalidOperationException($"card text is not transformed: {cardRaw}");
            if (!powerRaw.Contains("每有1个") ||
                !powerRaw.Contains("分别对所有敌人") ||
                !powerRaw.Contains("{Amount}"))
                throw new InvalidOperationException($"power text is not synchronized: {powerRaw}");

            await WithTimeout(OrbCmd.Channel<FrostOrb>(context, player), "channeling Frost");

            var console = new DevConsole(true);
            var addCard = console.ProcessCommand("card HAILSTORM");
            if (!addCard.success) throw new InvalidOperationException(addCard.msg);
            await Task.Delay(150);
            var hailstorm = PileType.Hand.GetPile(player).Cards.OfType<Hailstorm>().Last();
            if (hailstorm.DynamicVars["HailstormPower"].BaseValue != 3m)
                throw new InvalidOperationException($"base transformed amount expected 3, actual {hailstorm.DynamicVars["HailstormPower"].BaseValue}");
            await WithTimeout(CardCmd.AutoPlay(context, hailstorm, null), "playing transformed Hailstorm");
            var power = player.Creature.GetPower<HailstormPower>()
                ?? throw new InvalidOperationException("HailstormPower was not applied");
            if (power.Amount != 3m)
                throw new InvalidOperationException($"played power amount expected 3, actual {power.Amount}");

            var addUpgraded = console.ProcessCommand("card HAILSTORM");
            if (!addUpgraded.success) throw new InvalidOperationException(addUpgraded.msg);
            await Task.Delay(150);
            var upgraded = PileType.Hand.GetPile(player).Cards.OfType<Hailstorm>().Last();
            upgraded.UpgradeInternal();
            upgraded.FinalizeUpgradeInternal();
            if (upgraded.DynamicVars["HailstormPower"].BaseValue != 4m)
                throw new InvalidOperationException($"upgraded transformed amount expected 4, actual {upgraded.DynamicVars["HailstormPower"].BaseValue}");
            var enemies = power.CombatState.HittableEnemies.ToList();
            if (enemies.Count == 0)
                throw new InvalidOperationException("no hittable enemies");
            var frostCount = player.PlayerCombatState.OrbQueue.Orbs.Count(orb => orb is FrostOrb);
            if (frostCount < 1)
                throw new InvalidOperationException("no Frost orb was present before trigger");

            var before = history.Entries.OfType<DamageReceivedEntry>().Count();
            await WithTimeout(
                power.BeforeSideTurnEnd(context, player.Creature.Side, new[] { player.Creature }),
                "triggering transformed Hailstorm");
            var entries = history.Entries
                .OfType<DamageReceivedEntry>()
                .Skip(before)
                .Where(entry => entry.Dealer == player.Creature && enemies.Contains(entry.Receiver))
                .ToList();
            var expectedEvents = frostCount * enemies.Count;

            MainFile.Log.Info(
                $"[CodexHailstormLiveTest] OBSERVE frost={frostCount} enemies={enemies.Count} " +
                $"events={entries.Count} expected={expectedEvents} damages=[{string.Join(",", entries.Select(e => e.Result.TotalDamage))}]");

            if (entries.Count != expectedEvents)
                throw new InvalidOperationException(
                    $"expected {expectedEvents} separate damage entries, got {entries.Count}");
            // Target powers such as Flight/Intangible may reduce the recorded
            // final damage. The power Amount is the outgoing value; verify at
            // least one unmitigated target receives it and none exceeds it.
            if (!entries.Any(entry => entry.Result.TotalDamage == power.Amount) ||
                entries.Any(entry => entry.Result.TotalDamage <= 0m || entry.Result.TotalDamage > power.Amount))
                throw new InvalidOperationException(
                    $"damage events do not match outgoing amount {power.Amount} after valid mitigation");

            MainFile.Log.Info(
                $"[CodexHailstormLiveTest] PASS: base/upgraded values are 3/4; {frostCount} Frost orb(s) each dealt a separate 3-damage hit to every enemy.");
        }
        catch (Exception ex)
        {
            MainFile.Log.Error($"[CodexHailstormLiveTest] FAIL: {ex}");
        }
    }

    private static void EnsureHailstormTransformationEnabled()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == "BetterDefect")
            ?? throw new InvalidOperationException("BetterDefect assembly is not loaded");
        var oddsType = assembly.GetType("BetterDefect.BdCardUpgradeState", throwOnError: true)!;
        var localizationType = assembly.GetType("BetterDefect.BdLocalization", throwOnError: true)!;
        var hailstorm = ModelDb.Card<Hailstorm>();
        var isEnabled = oddsType.GetMethod(
            "IsCardVersionUpgraded",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new MissingMethodException(oddsType.FullName, "IsCardVersionUpgraded");
        var toggle = oddsType.GetMethod(
            "ToggleCardVersionUpgrade",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new MissingMethodException(oddsType.FullName, "ToggleCardVersionUpgrade");

        if (!(bool)(isEnabled.Invoke(null, new object?[] { hailstorm }) ?? false))
        {
            if (!(bool)(toggle.Invoke(null, new object?[] { hailstorm }) ?? false))
                throw new InvalidOperationException("could not enable transformed Hailstorm");
        }

        var versionsType = assembly.GetType("BetterDefect.BdCardVersionUpgrades", throwOnError: true)!;
        var refreshCanonical = versionsType.GetMethod(
            "RefreshCanonicalFor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new MissingMethodException(versionsType.FullName, "RefreshCanonicalFor");
        refreshCanonical.Invoke(null, new object?[] { hailstorm });

        var refresh = localizationType.GetMethod(
            "RefreshVersionSensitiveCardDescriptions",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)
            ?? throw new MissingMethodException(localizationType.FullName, "RefreshVersionSensitiveCardDescriptions");
        refresh.Invoke(null, null);
    }
}
