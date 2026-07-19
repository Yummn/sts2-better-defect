using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;

namespace BetterDefect;

[HarmonyPatch(typeof(Player), "PopulateStartingDeck")]
internal static class BetterDefectStarterDeckPatch
{
    private static void Postfix(Player __instance)
    {
        try
        {
            BetterDefectDeck.Apply(__instance);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[BetterDefect] failed while replacing starter Strike with Ball Lightning: {ex}");
        }
    }
}

internal static class BetterDefectDeck
{
    public static void Apply(Player player)
    {
        if (player.Character is not Defect)
            return;

        var deck = player.Deck;
        if (deck.Cards.Any(card => card is BallLightning))
        {
            MainFile.Logger.Info("[BetterDefect] starter deck already contains Ball Lightning; skipped duplicate replacement.");
            return;
        }

        var strike = deck.Cards.FirstOrDefault(card => card is StrikeDefect);
        if (strike == null)
        {
            MainFile.Logger.Warn("[BetterDefect] Defect starter deck had no StrikeDefect to replace; skipped.");
            return;
        }

        var ballLightningTemplate = FindBallLightningTemplate();
        if (ballLightningTemplate == null)
        {
            MainFile.Logger.Error("[BetterDefect] BallLightning template not found; starter deck unchanged.");
            return;
        }

        var runState = player.RunState as RunState;
        var canonical = ballLightningTemplate.CanonicalInstance ?? ballLightningTemplate;
        var ballLightning = runState != null ? runState.CreateCard(canonical, player) : canonical.ToMutable();
        ballLightning.FloorAddedToDeck = strike.FloorAddedToDeck ?? 1;

        var strikeIndex = FindCardIndex(deck, strike);
        deck.RemoveInternal(strike, silent: true);
        if (runState != null && runState.ContainsCard(strike))
            runState.RemoveCard(strike);

        deck.AddInternal(ballLightning, strikeIndex >= 0 ? strikeIndex : -1, silent: true);
        deck.InvokeCardRemoveFinished();
        deck.InvokeCardAddFinished();
        deck.InvokeContentsChanged();

        MainFile.Logger.Info($"[BetterDefect] replaced starter {SafeCardId(strike)} with {SafeCardId(ballLightning)}.");
    }

    private static CardModel? FindBallLightningTemplate()
    {
        try
        {
            var card = ModelDb.AllCards.FirstOrDefault(card => card is BallLightning);
            if (card != null)
                return card;
        }
        catch { }

        try
        {
            var byId = ModelDb.GetByIdOrNull<CardModel>(new ModelId("CARD", "BALL_LIGHTNING"));
            if (byId != null)
                return byId;
        }
        catch { }

        try
        {
            ModelDb.Inject(typeof(BallLightning));
            return ModelDb.AllCards.FirstOrDefault(card => card is BallLightning)
                ?? ModelDb.GetByIdOrNull<CardModel>(new ModelId("CARD", "BALL_LIGHTNING"));
        }
        catch { }

        try { return new BallLightning(); }
        catch { return null; }
    }

    private static string SafeCardId(CardModel card)
    {
        try { return card.Id.ToString(); }
        catch { return card.GetType().FullName ?? card.GetType().Name; }
    }

    private static int FindCardIndex(CardPile deck, CardModel target)
    {
        for (var i = 0; i < deck.Cards.Count; i++)
        {
            if (ReferenceEquals(deck.Cards[i], target))
                return i;
        }

        return -1;
    }
}
