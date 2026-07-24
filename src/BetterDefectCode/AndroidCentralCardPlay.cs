using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using BufferCard = MegaCrit.Sts2.Core.Models.Cards.Buffer;

namespace BetterDefect;

/// <summary>
/// Android v103 card-effect multiplexer. One detour on OnPlayWrapper's async
/// state machine replaces 29 separate virtual OnPlay detours. The original
/// wrapper still owns pile movement, hooks, history, replay, enchantments and
/// cleanup; only its single virtual OnPlay call is redirected here.
/// </summary>
[HarmonyPatch]
internal static class BdAndroidCentralCardPlayPatch
{
    private static MethodBase? TargetMethod()
    {
        var wrapper = AccessTools.DeclaredMethod(typeof(CardModel), nameof(CardModel.OnPlayWrapper));
        return wrapper is null ? null : AccessTools.AsyncMoveNext(wrapper);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.DeclaredMethod(
            typeof(CardModel),
            "OnPlay",
            [typeof(PlayerChoiceContext), typeof(CardPlay)]);
        var replacement = AccessTools.DeclaredMethod(
            typeof(BdAndroidCardPlayDispatcher),
            nameof(BdAndroidCardPlayDispatcher.OnPlay));
        var replaced = 0;

        foreach (var instruction in instructions)
        {
            if (original is not null && replacement is not null && instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced++;
            }
            yield return instruction;
        }

        if (replaced != 1)
            throw new InvalidOperationException($"Expected one CardModel.OnPlay call in OnPlayWrapper state machine, replaced {replaced}.");
    }
}

internal static class BdAndroidCardPlayDispatcher
{
    private static readonly MethodInfo OriginalOnPlay = AccessTools.DeclaredMethod(
        typeof(CardModel),
        "OnPlay",
        [typeof(PlayerChoiceContext), typeof(CardPlay)])
        ?? throw new MissingMethodException(typeof(CardModel).FullName, "OnPlay");

    internal static Task OnPlay(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // The old wrapper-prefix tracker counted a power card once even when a
        // replay effect made the wrapper execute OnPlay more than once.
        try
        {
            if (cardPlay.PlayIndex == 0 && card.Type == CardType.Power && card.Owner is not null)
                BdCombatTracker.For(card.Owner).PowerCardsPlayed++;
        }
        catch { }

        // Four historical fixes intentionally own both their baseline and
        // point-enabled behavior, matching their former dedicated patches.
        switch (card)
        {
            case Shatter typed:
                return BdCardVersionShatterPlayPatch.Play(typed, choiceContext);
            case TeslaCoil typed:
                return BdCardVersionTeslaCoilPlayPatch.Play(typed, choiceContext, cardPlay);
            case Fuel typed:
                return BdCardVersionFuelPlayPatch.Play(typed, choiceContext);
            case Scrape typed:
                return BdCardVersionScrapePlayPatch.Play(typed, choiceContext, cardPlay);
        }

        if (BdCustomCommonCardPlayPatch.TryPlay(card, choiceContext, cardPlay, out var commonTask))
            return commonTask;

        if (BdCardVersionUpgrades.IsVersionEnabled(card))
        {
            switch (card)
            {
                case AdaptiveStrike typed:
                    return BdCustomRareCardPlay.PlayAdaptiveStrike(typed, choiceContext, cardPlay);
                case AllForOne typed:
                    return BdCustomRareCardPlay.PlayAllForOne(typed, choiceContext, cardPlay);
                case BufferCard typed:
                    return BdCustomRareCardPlay.PlayBuffer(typed, choiceContext, cardPlay);
                case FlakCannon typed:
                    return BdCustomRareCardPlay.PlayFlakCannon(typed, choiceContext, cardPlay);
                case MeteorStrike typed:
                    return BdCustomRareCardPlay.PlayMeteorStrike(typed, choiceContext, cardPlay);
                case MultiCast typed:
                    return BdCustomRareCardPlay.PlayMultiCast(typed, choiceContext);
                case Rainbow typed:
                    return BdCustomRareCardPlay.PlayRainbow(typed, choiceContext);
            }
        }

        return (Task)(OriginalOnPlay.Invoke(card, [choiceContext, cardPlay])
            ?? Task.CompletedTask);
    }
}
