using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace BetterDefect.Cards;

internal static class Bd
{
    private static readonly object ReflectionCacheLock = new();
    private static readonly Dictionary<Type, MethodInfo> ApplyPowerWithContextByType = new();
    private static readonly Dictionary<Type, MethodInfo> ApplyPowerWithoutContextByType = new();
    private static readonly Dictionary<Type, (MethodInfo? Method, object? Arg)> OrbVisualUpdateByManagerType = new();
    private static readonly Dictionary<Type, PropertyInfo?> CombatStatePropertyByType = new();
    private static readonly Dictionary<Type, PropertyInfo?> HittableEnemiesPropertyByType = new();
    private static readonly Dictionary<Type, MethodInfo?> OpponentsMethodByType = new();
    private static readonly Dictionary<(Type AttackType, Type StateType), MethodInfo?> TargetAllOpponentsByType = new();
    private static readonly Dictionary<(Type StateType, Type CardType), MethodInfo?> CreateCardByType = new();
    private static MethodInfo? AddGeneratedCardToCombatMethod;
    private static MethodInfo? ModifyPowerWithContext;
    private static MethodInfo? ModifyPowerWithoutContext;

    /// <summary>
    /// Resolve combat state through reflection. PC v107 exposes ICombatState,
    /// while Android v103 exposes the concrete CombatState class. A direct
    /// CardModel.CombatState call compiled against v107 leaves an ICombatState
    /// token in the mod DLL and throws TypeLoadException on v103.
    /// </summary>
    public static object? CombatState(CardModel card)
    {
        try
        {
            var state = CombatStateFrom(card);
            if (state != null) return state;
            return card.Owner?.Creature is { } creature ? CombatStateFrom(creature) : null;
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect] failed to resolve cross-version combat state: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static object? CombatStateFrom(object source)
    {
        var sourceType = source.GetType();
        PropertyInfo? property;
        lock (ReflectionCacheLock)
        {
            if (!CombatStatePropertyByType.TryGetValue(sourceType, out property))
            {
                property = AccessTools.Property(sourceType, "CombatState");
                CombatStatePropertyByType[sourceType] = property;
            }
        }
        return property?.GetValue(source);
    }

    public static IReadOnlyList<Creature> Enemies(CardModel card)
    {
        try
        {
            var state = CombatState(card);
            if (state == null) return Array.Empty<Creature>();

            var stateType = state.GetType();
            PropertyInfo? property;
            lock (ReflectionCacheLock)
            {
                if (!HittableEnemiesPropertyByType.TryGetValue(stateType, out property))
                {
                    property = AccessTools.Property(stateType, "HittableEnemies");
                    HittableEnemiesPropertyByType[stateType] = property;
                }
            }

            return property?.GetValue(state) switch
            {
                IReadOnlyList<Creature> list => list,
                IEnumerable<Creature> sequence => sequence.ToList(),
                _ => Array.Empty<Creature>()
            };
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect] failed to resolve cross-version enemies: {ex.GetType().Name}: {ex.Message}");
            return Array.Empty<Creature>();
        }
    }

    public static T CreateCard<T>(CardModel source) where T : CardModel
    {
        var state = CombatState(source) ?? throw new InvalidOperationException("Combat state is unavailable.");
        var key = (state.GetType(), typeof(T));
        MethodInfo? method;
        lock (ReflectionCacheLock)
        {
            if (!CreateCardByType.TryGetValue(key, out method))
            {
                method = key.Item1.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(candidate => string.Equals(candidate.Name, "CreateCard", StringComparison.Ordinal))
                    .Where(candidate => candidate.IsGenericMethodDefinition && candidate.GetGenericArguments().Length == 1)
                    .FirstOrDefault(candidate =>
                    {
                        var parameters = candidate.GetParameters();
                        return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(source.Owner.GetType());
                    })
                    ?.MakeGenericMethod(typeof(T));
                CreateCardByType[key] = method;
            }
        }

        if (method?.Invoke(state, new object[] { source.Owner }) is T generated)
            return generated;
        throw new MissingMethodException(key.Item1.FullName, $"CreateCard<{typeof(T).Name}>");
    }

    public static Task<CardPileAddResult> AddGeneratedCardToCombat(CardModel card, PileType pile, Player creator)
    {
        AddGeneratedCardToCombatMethod ??= typeof(CardPileCmd)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                if (!string.Equals(method.Name, "AddGeneratedCardToCombat", StringComparison.Ordinal)) return false;
                var parameters = method.GetParameters();
                return parameters.Length == 4
                       && parameters[0].ParameterType.IsAssignableFrom(typeof(CardModel))
                       && parameters[1].ParameterType == typeof(PileType);
            });
        var method = AddGeneratedCardToCombatMethod
                     ?? throw new MissingMethodException(typeof(CardPileCmd).FullName, "AddGeneratedCardToCombat");
        var thirdType = method.GetParameters()[2].ParameterType;
        object thirdArg = thirdType == typeof(bool) ? true : creator;
        try
        {
            return (Task<CardPileAddResult>)method.Invoke(
                null,
                new object[] { card, pile, thirdArg, CardPilePosition.Bottom })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    public static IReadOnlyList<Creature> Opponents(Creature creature)
    {
        try
        {
            var state = CombatStateFrom(creature);
            if (state == null) return Array.Empty<Creature>();

            var stateType = state.GetType();
            MethodInfo? method;
            lock (ReflectionCacheLock)
            {
                if (!OpponentsMethodByType.TryGetValue(stateType, out method))
                {
                    method = stateType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(candidate =>
                        {
                            if (!string.Equals(candidate.Name, "GetOpponentsOf", StringComparison.Ordinal)) return false;
                            var parameters = candidate.GetParameters();
                            return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(creature.GetType());
                        });
                    OpponentsMethodByType[stateType] = method;
                }
            }

            return method?.Invoke(state, new object[] { creature }) switch
            {
                IReadOnlyList<Creature> list => list,
                IEnumerable<Creature> sequence => sequence.ToList(),
                _ => Array.Empty<Creature>()
            };
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect] failed to resolve cross-version opponents: {ex.GetType().Name}: {ex.Message}");
            return Array.Empty<Creature>();
        }
    }

    /// <summary>
    /// Calls AttackCommand.TargetingAllOpponents without embedding either the
    /// v107 ICombatState or v103 CombatState parameter type in BetterDefect.dll.
    /// </summary>
    public static bool TryTargetAllOpponents(object attackCommand, CardModel card)
    {
        try
        {
            var state = CombatState(card);
            if (state == null) return false;

            var key = (attackCommand.GetType(), state.GetType());
            MethodInfo? method;
            lock (ReflectionCacheLock)
            {
                if (!TargetAllOpponentsByType.TryGetValue(key, out method))
                {
                    method = key.Item1.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(candidate =>
                        {
                            if (!string.Equals(candidate.Name, "TargetingAllOpponents", StringComparison.Ordinal)) return false;
                            var parameters = candidate.GetParameters();
                            return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(key.Item2);
                        });
                    TargetAllOpponentsByType[key] = method;
                }
            }

            if (method == null) return false;
            method.Invoke(attackCommand, new[] { state });
            return true;
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect] failed to target all opponents across game versions: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
    public static Creature? RandomEnemy(CardModel card)
    {
        var enemies = Enemies(card);
        return enemies.Count == 0 ? null : card.Owner.RunState.Rng.CombatTargets.NextItem(enemies);
    }
    public static void RemoveOrbWithoutEvoke(CardModel card, OrbModel orb)
    {
        var player = card.Owner;
        var manager = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager;
        RemoveOrbWithoutEvoke(player, manager, orb);
    }

    public static void RemoveOrbWithoutEvoke(Player player, object? manager, OrbModel orb)
    {
        try
        {
            var queue = player.PlayerCombatState.OrbQueue;
            if (!queue.Remove(orb)) return;

            try { manager?.GetType().GetMethod("EvokeOrbAnim")?.Invoke(manager, new object[] { orb }); } catch { }
            orb.RemoveInternal();
            TryRefreshOrbVisuals(manager);
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect] failed to remove orb for Fission: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void RefreshOrbVisuals(CardModel card)
    {
        try
        {
            var manager = NCombatRoom.Instance?.GetCreatureNode(card.Owner.Creature)?.OrbManager;
            TryRefreshOrbVisuals(manager);
        }
        catch { }
    }

    private static void TryRefreshOrbVisuals(object? manager)
    {
        if (manager == null) return;

        try
        {
            var resolved = ResolveOrbVisualUpdate(manager.GetType());
            if (resolved.Method != null)
                resolved.Method.Invoke(manager, new[] { resolved.Arg });
        }
        catch { }
    }
    public static Task Damage(PlayerChoiceContext ctx, CardModel card, Creature? target, DamageVar damage) =>
        target == null ? Task.CompletedTask : CreatureCmd.Damage(ctx, target, damage, card);
    public static Task DamageAll(PlayerChoiceContext ctx, CardModel card, DamageVar damage) =>
        CreatureCmd.Damage(ctx, Enemies(card), damage, card.Owner.Creature, card);
    public static Task Block(CardModel card, CardPlay play) => CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, play);
    public static decimal Var(CardModel card, string key) => card.DynamicVars[key].BaseValue;
    public static int CostForEnergy(CardModel card)
    {
        // StS1 Recycle treats an X-cost card as its current X value. Reading the
        // player's remaining energy also keeps Chemical X and card-cost state out
        // of this calculation: Recycle refunds the printed/resolved energy value,
        // it does not actually play the selected card.
        if (card.EnergyCost.CostsX)
            return Math.Max(0, card.Owner.PlayerCombatState.Energy);
        return Math.Max(0, card.EnergyCost.GetResolved());
    }

    public static Task ApplyPower<T>(PlayerChoiceContext ctx, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel
    {
        try
        {
            var withContext = ResolveApplyPowerMethod(typeof(T), withContext: true);
            if (withContext != null)
                return (Task)withContext.Invoke(null, new object?[] { ctx, target, amount, applier, cardSource, silent })!;

            var withoutContext = ResolveApplyPowerMethod(typeof(T), withContext: false);
            if (withoutContext != null)
                return (Task)withoutContext.Invoke(null, new object?[] { target, amount, applier, cardSource, silent })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
        throw new MissingMethodException("PowerCmd.Apply compatible overload not found");
    }

    public static Task ModifyPowerAmount(PlayerChoiceContext ctx, PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent = false)
    {
        try
        {
            var withContext = ResolveModifyPowerMethod(withContext: true);
            if (withContext != null)
                return (Task)withContext.Invoke(null, new object?[] { ctx, power, offset, applier, cardSource, silent })!;

            var withoutContext = ResolveModifyPowerMethod(withContext: false);
            if (withoutContext != null)
                return (Task)withoutContext.Invoke(null, new object?[] { power, offset, applier, cardSource, silent })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
        throw new MissingMethodException("PowerCmd.ModifyAmount compatible overload not found");
    }

    private static (MethodInfo? Method, object? Arg) ResolveOrbVisualUpdate(Type managerType)
    {
        lock (ReflectionCacheLock)
        {
            if (OrbVisualUpdateByManagerType.TryGetValue(managerType, out var cached))
                return cached;
        }

        MethodInfo? method = null;
        object? arg = null;
        try
        {
            method = managerType.GetMethods().FirstOrDefault(m => m.Name == "UpdateVisuals" && m.GetParameters().Length == 1);
            var paramType = method?.GetParameters()[0].ParameterType;
            if (method != null && paramType != null)
                arg = paramType.IsEnum ? Enum.ToObject(paramType, 0) : Activator.CreateInstance(paramType);
        }
        catch
        {
            method = null;
            arg = null;
        }

        lock (ReflectionCacheLock)
        {
            OrbVisualUpdateByManagerType[managerType] = (method, arg);
        }
        return (method, arg);
    }

    private static MethodInfo? ResolveApplyPowerMethod(Type powerType, bool withContext)
    {
        var cache = withContext ? ApplyPowerWithContextByType : ApplyPowerWithoutContextByType;
        lock (ReflectionCacheLock)
        {
            if (cache.TryGetValue(powerType, out var cached))
                return cached;
        }

        MethodInfo? resolved = null;
        foreach (var method in typeof(PowerCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name == "Apply" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1))
        {
            var ps = method.GetParameters();
            if (withContext)
            {
                if (ps.Length >= 6 && ps[0].ParameterType.IsAssignableFrom(typeof(PlayerChoiceContext)) && ps[1].ParameterType.IsAssignableFrom(typeof(Creature)))
                {
                    resolved = method.MakeGenericMethod(powerType);
                    break;
                }
            }
            else if (ps.Length >= 5 && ps[0].ParameterType.IsAssignableFrom(typeof(Creature)))
            {
                resolved = method.MakeGenericMethod(powerType);
                break;
            }
        }

        if (resolved != null)
        {
            lock (ReflectionCacheLock)
            {
                cache[powerType] = resolved;
            }
        }
        return resolved;
    }

    private static MethodInfo? ResolveModifyPowerMethod(bool withContext)
    {
        lock (ReflectionCacheLock)
        {
            var cached = withContext ? ModifyPowerWithContext : ModifyPowerWithoutContext;
            if (cached != null)
                return cached;
        }

        MethodInfo? resolved = null;
        foreach (var method in typeof(PowerCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name == "ModifyAmount" && !m.IsGenericMethodDefinition))
        {
            var ps = method.GetParameters();
            if (withContext)
            {
                if (ps.Length >= 6 && ps[0].ParameterType.IsAssignableFrom(typeof(PlayerChoiceContext)) && ps[1].ParameterType.IsAssignableFrom(typeof(PowerModel)))
                {
                    resolved = method;
                    break;
                }
            }
            else if (ps.Length >= 5 && ps[0].ParameterType.IsAssignableFrom(typeof(PowerModel)))
            {
                resolved = method;
                break;
            }
        }

        lock (ReflectionCacheLock)
        {
            if (withContext)
                ModifyPowerWithContext = resolved;
            else
                ModifyPowerWithoutContext = resolved;
        }
        return resolved;
    }
}

public sealed class BdRecursion : CardModel
{
    public BdRecursion() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var orb = Owner.PlayerCombatState.OrbQueue.Orbs.FirstOrDefault();
        if (orb == null) return;
        var t = orb.GetType();
        if (BetterDefect.BdCardVersionUpgrades.IsVersionEnabled(this))
        {
            // Recursion explicitly operates on the left/front orb. EvokeNext
            // uses OrbQueue.Orbs.First(), unlike EvokeLast used by effects
            // that operate from the right side. The first call keeps the orb
            // in place so the same object is evoked twice; the second removes
            // it before the same orb type is re-channeled.
            await OrbCmd.EvokeNext(choiceContext, Owner, dequeue: false);
            await OrbCmd.EvokeNext(choiceContext, Owner);
        }
        else
        {
            await OrbCmd.EvokeNext(choiceContext, Owner);
        }
        if (t == typeof(LightningOrb)) await OrbCmd.Channel<LightningOrb>(choiceContext, Owner);
        else if (t == typeof(FrostOrb)) await OrbCmd.Channel<FrostOrb>(choiceContext, Owner);
        else if (t == typeof(DarkOrb)) await OrbCmd.Channel<DarkOrb>(choiceContext, Owner);
        else await OrbCmd.Channel(choiceContext, (OrbModel)Activator.CreateInstance(t)!, Owner);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class BdSteamBarrier : CardModel
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new BlockVar(6, ValueProp.Move) };
    public BdSteamBarrier() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.Block(this, cardPlay);
        DynamicVars.Block.BaseValue = Math.Max(0, DynamicVars.Block.BaseValue - 1);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

public sealed class BdStreamline : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(15, ValueProp.Move) };
    public BdStreamline() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.Damage(choiceContext, this, cardPlay.Target, DynamicVars.Damage);
        if (BetterDefect.BdCardVersionUpgrades.IsVersionEnabled(this))
        {
            foreach (var streamline in Owner.PlayerCombatState.AllCards.OfType<BdStreamline>())
                streamline.EnergyCost.AddThisCombat(-1, reduceOnly: true);
        }
        else
        {
            EnergyCost.AddThisCombat(-1, reduceOnly: true);
        }
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5);
}

public sealed class BdAggregate : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Divisor", 4) };
    public BdAggregate() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var divisor = Math.Max(1, (int)DynamicVars["Divisor"].BaseValue);
        Owner.PlayerCombatState.GainEnergy(Owner.PlayerCombatState.DrawPile.Cards.Count / divisor);
        return Task.CompletedTask;
    }
    protected override void OnUpgrade() => DynamicVars["Divisor"].UpgradeValueBy(-1);
}

public sealed class BdAutoShields : CardModel
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new BlockVar(11, ValueProp.Move) };
    public BdAutoShields() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Owner.Creature.Block <= 0 ? Bd.Block(this, cardPlay) : Task.CompletedTask;
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

public sealed class BdBlizzard : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(2, ValueProp.Move) };
    public BdBlizzard() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var amount = DynamicVars.Damage.BaseValue * BetterDefect.BdCombatTracker.For(Owner).FrostChanneled;
        return CreatureCmd.Damage(choiceContext, Bd.Enemies(this), amount, ValueProp.Move, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

public sealed class BdBullseye : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(8, ValueProp.Move), new DynamicVar("LockOn", 2) };
    public BdBullseye() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.Damage(choiceContext, this, cardPlay.Target, DynamicVars.Damage);
        if (cardPlay.Target != null) await Bd.ApplyPower<BdLockOnPower>(choiceContext, cardPlay.Target, DynamicVars["LockOn"].BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(3); DynamicVars["LockOn"].UpgradeValueBy(1); }
}

public sealed class BdConsume : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Focus", 2) };
    public BdConsume() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.ApplyPower<FocusPower>(choiceContext, Owner.Creature, DynamicVars["Focus"].BaseValue, Owner.Creature, this);
        OrbCmd.RemoveSlots(Owner, 1);
    }
    protected override void OnUpgrade() => DynamicVars["Focus"].UpgradeValueBy(1);
}

public sealed class BdDoomAndGloom : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(10, ValueProp.Move) };
    public BdDoomAndGloom() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.DamageAll(choiceContext, this, DynamicVars.Damage);
        await OrbCmd.Channel<DarkOrb>(choiceContext, Owner);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

public sealed class BdForceField : CardModel
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new BlockVar(12, ValueProp.Move) };
    public BdForceField() : base(4, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Bd.Block(this, cardPlay);
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (ReferenceEquals(card, this))
        {
            modifiedCost = Math.Max(0, originalCost - BetterDefect.BdCombatTracker.For(Owner).PowerCardsPlayed);
            return true;
        }
        modifiedCost = originalCost;
        return false;
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

public sealed class BdHeatsinks : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Draw", 1) };
    public BdHeatsinks() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Bd.ApplyPower<BdHeatsinksPower>(choiceContext, Owner.Creature, DynamicVars["Draw"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["Draw"].UpgradeValueBy(1);
}

public sealed class BdMelter : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(10, ValueProp.Move) };
    public BdMelter() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target != null)
        {
            if (cardPlay.Target.Block > 0) await CreatureCmd.LoseBlock(cardPlay.Target, cardPlay.Target.Block);
            await Bd.Damage(choiceContext, this, cardPlay.Target, DynamicVars.Damage);
        }
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

public sealed class BdRecycle : CardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public BdRecycle() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var victim = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            null,
            this)).FirstOrDefault();
        if (victim == null) return;
        var energy = Bd.CostForEnergy(victim);
        await CardCmd.Exhaust(choiceContext, victim);
        Owner.PlayerCombatState.GainEnergy(energy);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class BdReinforcedBody : CardModel
{
    protected override bool HasEnergyCostX => true;
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new BlockVar(7, ValueProp.Move) };
    public BdReinforcedBody() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var x = ResolveEnergyXValue();
        for (var i = 0; i < x; i++) await Bd.Block(this, cardPlay);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

public sealed class BdReprogram : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Focus", 1), new PowerVar<StrengthPower>(1), new PowerVar<DexterityPower>(1) };
    public BdReprogram() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.ApplyPower<FocusPower>(choiceContext, Owner.Creature, -DynamicVars["Focus"].BaseValue, Owner.Creature, this);
        await Bd.ApplyPower<StrengthPower>(choiceContext, Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, this);
        await Bd.ApplyPower<DexterityPower>(choiceContext, Owner.Creature, DynamicVars.Dexterity.BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade()
    {
        // Reprogram+ still loses exactly 1 Focus. Only Strength and
        // Dexterity improve from 1 to 2.
        DynamicVars.Strength.UpgradeValueBy(1);
        DynamicVars.Dexterity.UpgradeValueBy(1);
    }
}

public sealed class BdSelfRepair : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new HealVar(7) };
    public BdSelfRepair() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Bd.ApplyPower<BdSelfRepairPower>(choiceContext, Owner.Creature, DynamicVars.Heal.BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars.Heal.UpgradeValueBy(3);
}

public sealed class BdStaticDischarge : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Amount", 1) };
    public BdStaticDischarge() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Bd.ApplyPower<BdStaticDischargePower>(choiceContext, Owner.Creature, DynamicVars["Amount"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["Amount"].UpgradeValueBy(1);
}

public sealed class BdSeek : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Amount", 1) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public BdSeek() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawPile = PileType.Draw.GetPile(Owner);
        var count = (int)DynamicVars["Amount"].BaseValue;
        var selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            drawPile.Cards,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, count))).ToList();

        foreach (var card in selected)
            await CardPileCmd.Add(card, PileType.Hand);
    }
    protected override void OnUpgrade() => DynamicVars["Amount"].UpgradeValueBy(1);
}

public sealed class BdCoreSurge : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(11, ValueProp.Move) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public BdCoreSurge() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.Damage(choiceContext, this, cardPlay.Target, DynamicVars.Damage);
        await Bd.ApplyPower<ArtifactPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

public sealed class BdElectrodynamics : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Amount", 2) };
    public BdElectrodynamics() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Bd.ApplyPower<BdElectrodynamicsPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
        for (var i = 0; i < (int)DynamicVars["Amount"].BaseValue; i++) await OrbCmd.Channel<LightningOrb>(choiceContext, Owner);
    }
    protected override void OnUpgrade() => DynamicVars["Amount"].UpgradeValueBy(1);
}

public sealed class BdFission : CardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public BdFission() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var orbCount = Owner.PlayerCombatState.OrbQueue.Orbs.Count;
        for (var i = 0; i < orbCount; i++)
        {
            if (IsUpgraded)
            {
                await OrbCmd.EvokeNext(choiceContext, Owner);
            }
            else
            {
                var orb = Owner.PlayerCombatState.OrbQueue.Orbs.FirstOrDefault();
                if (orb == null) break;
                Bd.RemoveOrbWithoutEvoke(this, orb);
            }
            Owner.PlayerCombatState.GainEnergy(1);
            await CardPileCmd.Draw(choiceContext, Owner);
        }
        Bd.RefreshOrbVisuals(this);
    }
    protected override void OnUpgrade() { }
}

public sealed class BdThunderStrike : CardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(7, ValueProp.Move) };
    public BdThunderStrike() : base(3, CardType.Attack, CardRarity.Rare, TargetType.RandomEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hits = BetterDefect.BdCombatTracker.For(Owner).LightningChanneled;
        for (var i = 0; i < hits; i++)
        {
            var target = Bd.RandomEnemy(this);
            if (target == null) break;
            await CreatureCmd.Damage(choiceContext, target, DynamicVars.Damage, this);
        }
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

public sealed class BdHeatsinksPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Power)
            return CardPileCmd.Draw(context, Amount, Owner.Player).ContinueWith(_ => { });
        return Task.CompletedTask;
    }
}

public sealed class BdSelfRepairPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Combat teardown removes all powers before AfterCombatVictory is
    // dispatched. Heal from AfterCombatEnd instead, while this power is still
    // registered as a hook listener. This ordering is shared by Android v103
    // and PC v107.1.
    public override Task AfterCombatEnd(CombatRoom room) => CreatureCmd.Heal(Owner, Amount);
}

public sealed class BdStaticDischargePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // Only real attack/monster-move damage should trigger Static Discharge.
        // Poison, orb/relic damage and other HP-loss effects are Unpowered or
        // Unblockable and must not create Lightning.
        if (target != Owner || result.UnblockedDamage <= 0 || dealer == null) return;
        if ((props & ValueProp.Move) == 0 || (props & (ValueProp.Unpowered | ValueProp.Unblockable)) != 0) return;
        for (var i = 0; i < Amount; i++) await OrbCmd.Channel<LightningOrb>(choiceContext, Owner.Player);
    }
}

public sealed class BdElectrodynamicsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class BdLockOnPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // Damage modifiers return a multiplier, not the already multiplied
        // damage value. Orb damage is Unpowered and has no card source.
        if (target == Owner && dealer?.IsPlayer == true && cardSource == null && (props & ValueProp.Unpowered) != 0)
            return 1.5m;
        return 1m;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // Lock-On is a duration measured in enemy turns, not charges consumed by
        // individual orb hits.
        if (side == CombatSide.Enemy) return PowerCmd.TickDownDuration(this);
        return Task.CompletedTask;
    }
}

[HarmonyPatch]
internal static class BdElectrodynamicsLightningTargetPatch
{
    private static MethodBase? TargetMethod() => AccessTools.DeclaredMethod(typeof(LightningOrb), "ApplyLightningDamage");

    private static bool Prefix(
        LightningOrb __instance,
        decimal value,
        Creature? target,
        PlayerChoiceContext choiceContext,
        ref Task<IEnumerable<Creature>> __result)
    {
        try
        {
            if (__instance.Owner.Creature.GetPower<BdElectrodynamicsPower>() == null)
                return true;

            __result = DamageAll(__instance, value, choiceContext);
            return false;
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect] Electrodynamics target patch fell back to vanilla Lightning: {ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }

    private static async Task<IEnumerable<Creature>> DamageAll(
        LightningOrb orb,
        decimal value,
        PlayerChoiceContext choiceContext)
    {
        var targets = Bd.Opponents(orb.Owner.Creature)
            .Where(enemy => enemy.IsHittable)
            .ToList();
        if (targets.Count == 0)
            return Array.Empty<Creature>();

        foreach (var enemy in targets)
            VfxCmd.PlayOnCreature(enemy, "vfx/vfx_attack_lightning");

        SfxCmd.Play("event:/sfx/characters/defect/defect_lightning_evoke");
        await CreatureCmd.Damage(choiceContext, targets, value, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }
}

[HarmonyPatch]
internal static class BdFissionSelfTestPatch
{
    private static bool Prepare() => string.Equals(System.Environment.GetEnvironmentVariable("BETTERDEFECT_TEST_FISSION"), "1", StringComparison.Ordinal) && AccessTools.Method(typeof(NOrbManager), "OnCombatSetup") != null;
    private static MethodBase? TargetMethod() => AccessTools.Method(typeof(NOrbManager), "OnCombatSetup");
    private static bool _done;

    private static void Postfix(NOrbManager __instance)
    {
        if (_done) return;
        if (!string.Equals(System.Environment.GetEnvironmentVariable("BETTERDEFECT_TEST_FISSION"), "1", StringComparison.Ordinal)) return;
        _done = true;

        try
        {
            var player = TryGetPlayer(__instance);
            if (player == null)
            {
                BetterDefect.MainFile.Logger.Warn("[BetterDefect:FissionSelfTest] skipped: could not resolve NOrbManager player.");
                return;
            }
            var queue = player.PlayerCombatState.OrbQueue;
            if (queue.Capacity < 2)
            {
                var add = 2 - queue.Capacity;
                queue.AddCapacity(add);
                __instance.AddSlotAnim(add);
            }

            var l1 = ModelDb.Orb<LightningOrb>().ToMutable();
            l1.Owner = player;
            AddOrbForSelfTest(queue, l1);
            __instance.AddOrbAnim();

            var l2 = ModelDb.Orb<LightningOrb>().ToMutable();
            l2.Owner = player;
            AddOrbForSelfTest(queue, l2);
            __instance.AddOrbAnim();

            LogState("before", __instance, queue.Orbs.Count);

            foreach (var orb in queue.Orbs.ToList())
            {
                Bd.RemoveOrbWithoutEvoke(player, __instance, orb);
                player.PlayerCombatState.GainEnergy(1);
            }

            LogState("after-immediate", __instance, queue.Orbs.Count);
            __instance.GetTree().CreateTimer(0.8).Timeout += () => LogState("after-0.8s", __instance, queue.Orbs.Count);
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Error($"[BetterDefect:FissionSelfTest] failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void LogState(string phase, NOrbManager manager, int queueCount)
    {
        try
        {
            var orbs = TryGetVisualOrbs(manager).ToList();
            var total = orbs.Count;
            var filled = orbs.Count(o => GetVisualOrbModel(o) != null);
            BetterDefect.MainFile.Logger.Info($"[BetterDefect:FissionSelfTest] {phase}: queue={queueCount}, visualTotal={total}, visualFilled={filled}");
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect:FissionSelfTest] log failed at {phase}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AddOrbForSelfTest(OrbQueue queue, OrbModel orb)
    {
        try
        {
            orb.AssertMutable();
            if (queue.Orbs.Count >= queue.Capacity) return;
            var list = AccessTools.Field(typeof(OrbQueue), "_orbs")?.GetValue(queue) as List<OrbModel>;
            list?.Add(orb);
        }
        catch (Exception ex)
        {
            BetterDefect.MainFile.Logger.Warn($"[BetterDefect:FissionSelfTest] failed to enqueue test orb: {ex.GetType().Name}: {ex.Message}");
        }
    }


    private static Player? TryGetPlayer(NOrbManager manager)
    {
        try
        {
            var creatureNode = AccessTools.Field(typeof(NOrbManager), "_creatureNode")?.GetValue(manager) as NCreature;
            return creatureNode?.Entity?.Player;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<object> TryGetVisualOrbs(NOrbManager manager)
    {
        try
        {
            return (AccessTools.Field(typeof(NOrbManager), "_orbs")?.GetValue(manager) as System.Collections.IEnumerable)?
                .Cast<object>() ?? Array.Empty<object>();
        }
        catch
        {
            return Array.Empty<object>();
        }
    }

    private static object? GetVisualOrbModel(object visualOrb)
    {
        try
        {
            return visualOrb.GetType()
                .GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
                .GetValue(visualOrb);
        }
        catch
        {
            return null;
        }
    }
}


