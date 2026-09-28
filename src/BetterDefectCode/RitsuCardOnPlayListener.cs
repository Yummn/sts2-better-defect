#if STS2_V111
using STS2RitsuLib.Cards;

namespace BetterDefect;

/// <summary>
/// On v0.111 RitsuLib already redirects CardModel.OnPlayWrapper into its
/// before/after hook dispatcher. Participate in that dispatcher rather than
/// applying a competing transpiler to the same async state machine.
/// </summary>
internal sealed class BdRitsuCardOnPlayListener : ICardOnPlayHookListener
{
    // Runtime contract for simulation mods: a matching version is not enough
    // if the RitsuLib callback failed to register during this game process.
    internal static bool IsRegistered { get; private set; }

    internal static bool TryRegister()
    {
        try
        {
            CardOnPlayHook.RegisterGlobalListener(new BdRitsuCardOnPlayListener());
            IsRegistered = true;
            MainFile.Logger.Info("[BetterDefect] registered v111 RitsuLib card-play listener; native before/after hooks preserved.");
            return true;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] RitsuLib card-play listener unavailable: {ex}");
            return false;
        }
    }

    public async Task<bool> BeforeCardOnPlay(BeforeCardOnPlayContext context)
    {
        var task = BdAndroidCardPlayDispatcher.TryOnPlay(
            context.CardPlay.Card,
            context.ChoiceContext,
            context.CardPlay);
        if (task is null)
            return false;

        await task;
        return true;
    }
}
#endif
