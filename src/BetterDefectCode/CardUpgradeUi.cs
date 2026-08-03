using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using System.Reflection;

namespace BetterDefect;

/// <summary>
/// Encyclopedia-only UI for BetterDefect card transformations.  Dynamic odds,
/// card disabling, grey masks and probability text are deliberately absent.
/// </summary>
internal static class BdCardUpgradeUi
{
    private const string UpgradeButtonName = "BetterDefectCardVersionUpgradeButton";
    private const string UiTouchedMeta = "better_defect_upgrade_ui_touched";
    private const string LegacyDisableButtonName = "BetterDefectDynamicOddsDisableButton";
    private const string LegacyDisabledOverlayName = "BetterDefectDynamicOddsDisabledOverlay";
    private static readonly Vector2 ButtonSize = new(176f, 56f);
    private static readonly string[] NonLibraryContextNeedles =
    [
        "NCombat", "CombatRoom", "Reward", "Merchant", "Shop", "InspectCard",
        "NCardPileScreen", "CardPileScreen", "DeckScreen", "DeckView",
        "DrawPile", "DiscardPile", "ExhaustPile", "MasterDeck"
    ];

    private static readonly FieldInfo? GridRowsField = AccessTools.Field(typeof(NCardGrid), "_cardRows");
    private static readonly FieldInfo? LibraryGridField = AccessTools.Field(typeof(NCardLibrary), "_grid");
    private static readonly FieldInfo? GridHolderBaseCardField = AccessTools.Field(typeof(NGridCardHolder), "_baseCard");
    private static readonly MethodInfo? GridHolderUpdateCardModelMethod = AccessTools.Method(typeof(NGridCardHolder), "UpdateCardModel");
    private static string? _lastToggleKey;
    private static ulong _lastToggleMsec;

    internal static void ApplyLibraryGrid(NCardLibraryGrid grid)
    {
        try
        {
            if (!IsCardLibraryContext(grid))
            {
                CleanupGrid(grid);
                BdCardUpgradeStatsHud.HideIfOutsideLibrary(grid);
                return;
            }

            if (OldDefectCards.RefreshCardLibraryGridIfStale(grid))
            {
                for (Node? current = grid.GetParent(); current != null; current = current.GetParent())
                {
                    if (current is not NCardLibrary library)
                        continue;
                    Callable.From(() => AccessTools.Method(typeof(NCardLibrary), "UpdateFilter")?.Invoke(library, [false])).CallDeferred();
                    break;
                }
            }

            if (GridRowsField?.GetValue(grid) is IEnumerable<List<NGridCardHolder>> rows)
            {
                foreach (var row in rows)
                    ApplyLibraryRow(row, verifiedLibrary: true);
            }
            BdCardUpgradeStatsHud.ShowForLibrary(grid);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] transformation library UI skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void ApplyLibraryRowForGrid(NCardLibraryGrid grid, IEnumerable<NGridCardHolder>? row)
    {
        if (row is null)
            return;
        if (!IsCardLibraryContext(grid))
        {
            foreach (var holder in row)
                if (holder?.CardNode is { } card)
                    RemoveUi(card);
            return;
        }
        ApplyLibraryRow(row, verifiedLibrary: true);
    }

    private static void ApplyLibraryRow(IEnumerable<NGridCardHolder> row, bool verifiedLibrary)
    {
        foreach (var holder in row)
        {
            try
            {
                var cardNode = holder?.CardNode;
                if (cardNode is null)
                    continue;
                if (holder is CanvasItem item && !item.Visible)
                {
                    RemoveUi(cardNode);
                    continue;
                }
                ApplyLibraryCardUi(cardNode, verifiedLibrary);
            }
            catch { }
        }
    }

    internal static void ApplyLibraryCardUi(NCard cardNode, bool assumeLibrary = false)
    {
        try
        {
            if (!assumeLibrary && !IsCardLibraryContext(cardNode))
            {
                RemoveUi(cardNode);
                return;
            }

            RemoveLegacyOddsArtifacts(cardNode);
            var card = cardNode.Model;
            if (card is null || !BdCardVersionUpgrades.IsEligible(card))
            {
                RemoveUpgradeButton(cardNode);
                return;
            }
            EnsureUpgradeButton(cardNode, card);
            BdCardUpgradeStatsHud.ShowForLibrary(cardNode);
        }
        catch { }
    }

    internal static void EnforceCardUiScope(NCard cardNode)
    {
        try
        {
            if (!IsCardLibraryContext(cardNode))
                RemoveUi(cardNode);
        }
        catch { }
    }

    private static void EnsureUpgradeButton(NCard cardNode, CardModel card)
    {
        var enabled = BdCardUpgradeState.IsCardVersionUpgraded(card);
        var button = cardNode.GetNodeOrNull<Button>(UpgradeButtonName);
        if (button is null)
        {
            cardNode.SetMeta(UiTouchedMeta, Variant.From(true));
            button = new Button
            {
                Name = UpgradeButtonName,
                Size = ButtonSize,
                CustomMinimumSize = ButtonSize,
                Position = new Vector2(28f, -76f),
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 41,
            };
            void Toggle() => ToggleFromButton(cardNode);
            button.ButtonDown += Toggle;
            button.Pressed += Toggle;
            button.GuiInput += input =>
            {
                if (input is InputEventScreenTouch touch && touch.Pressed)
                {
                    button.AcceptEvent();
                    ToggleFromButton(cardNode);
                }
                else if (input is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
                {
                    button.AcceptEvent();
                    ToggleFromButton(cardNode);
                }
            };
            cardNode.AddChild(button);
        }

        var used = BdCardUpgradeState.GetUsedCardPointCount();
        var full = !enabled && used >= BdCardUpgradeState.MaxCardPointBudget;
        var target = BdCardVersionUpgrades.GetTargetVersionLabel(card);
        button.Disabled = full;
        button.Text = enabled
            ? target.StartsWith("改造：", StringComparison.Ordinal) ? target : $"已改：{target}"
            : full ? "改造：点数已满" : "改造：关闭";
        button.TooltipText = full
            ? $"BetterDefect：改造点数已满（{used}/{BdCardUpgradeState.MaxCardPointBudget}），请先关闭一个改造。"
            : $"BetterDefect：消耗1点改造点数，切换到 {target}。效果：{BdCardVersionUpgrades.GetTargetEffectSummary(card)}。";
        button.Size = ButtonSize;
        button.CustomMinimumSize = ButtonSize;
        button.Position = new Vector2(28f, -76f);
        button.Visible = true;
        ApplyButtonStyle(button, enabled);
    }

    private static void ToggleFromButton(NCard cardNode)
    {
        var model = cardNode.Model;
        if (model is null)
            return;

        var key = SafeId(model);
        var now = Time.GetTicksMsec();
        if (string.Equals(_lastToggleKey, key, StringComparison.Ordinal) && now - _lastToggleMsec < 350UL)
            return;
        _lastToggleKey = key;
        _lastToggleMsec = now;

        if (!BdCardUpgradeState.ToggleCardVersionUpgrade(model))
        {
            BdCardUpgradeStatsHud.RefreshFrom(cardNode);
            return;
        }

        BdCardVersionUpgrades.RefreshCanonicalFor(model);
        RebuildGridHolderUpgradeCache(cardNode, model.GetType());
        RefreshCardsOfType(cardNode.GetTree()?.CurrentScene ?? cardNode, model.GetType());
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(cardNode) || cardNode.Model is null)
                return;
            BdCardVersionUpgrades.ApplyToModel(cardNode.Model);
            if (cardNode.IsNodeReady())
                cardNode.UpdateVisuals(cardNode.DisplayingPile, CardPreviewMode.Normal);
            ApplyLibraryCardUi(cardNode);
        }).CallDeferred();
        BdCardUpgradeStatsHud.RefreshFrom(cardNode);
    }

    private static void RebuildGridHolderUpgradeCache(NCard source, Type type)
    {
        try
        {
            NGridCardHolder? holder = null;
            for (var parent = source.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent is NGridCardHolder candidate)
                {
                    holder = candidate;
                    break;
                }
            }
            if (holder is null || GridHolderUpdateCardModelMethod is null)
                return;
            if (GridHolderBaseCardField?.GetValue(holder) is not CardModel baseCard || baseCard.GetType() != type)
                return;
            BdCardVersionUpgrades.ApplyToModel(baseCard);
            if (!ReferenceEquals(source.Model, baseCard))
                source.Model = baseCard;
            GridHolderUpdateCardModelMethod.Invoke(holder, null);
        }
        catch { }
    }

    private static void RefreshCardsOfType(Node root, Type type)
    {
        try
        {
            if (root is NCard cardNode && cardNode.Model?.GetType() == type)
            {
                BdCardVersionUpgrades.ApplyToModel(cardNode.Model);
                if (cardNode.IsNodeReady())
                    cardNode.UpdateVisuals(cardNode.DisplayingPile, CardPreviewMode.Normal);
                ApplyLibraryCardUi(cardNode);
            }
            foreach (var child in root.GetChildren())
                RefreshCardsOfType(child, type);
        }
        catch { }
    }

    internal static void ReapplyAfterUpgradePreviewRefresh(NGridCardHolder holder)
    {
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(holder) && holder.CardNode is { } card)
                ApplyLibraryCardUi(card);
        }).CallDeferred();
    }

    private static void ApplyButtonStyle(Button button, bool enabled)
    {
        var font = enabled ? new Color(0.82f, 1f, 0.72f) : new Color(1f, 0.78f, 0.67f);
        var background = enabled ? new Color(0.09f, 0.31f, 0.12f, 0.95f) : new Color(0.38f, 0.08f, 0.055f, 0.95f);
        var border = enabled ? new Color(0.43f, 0.82f, 0.31f) : new Color(0.92f, 0.31f, 0.20f);
        button.ClipText = true;
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", font);
        button.AddThemeColorOverride("font_hover_color", font.Lightened(0.12f));
        button.AddThemeColorOverride("font_pressed_color", font.Darkened(0.18f));
        button.AddThemeColorOverride("font_disabled_color", font.Darkened(0.35f));
        button.AddThemeStyleboxOverride("normal", MakeStyle(background, border, 3));
        button.AddThemeStyleboxOverride("hover", MakeStyle(background.Lightened(0.1f), border.Lightened(0.1f), 4));
        button.AddThemeStyleboxOverride("pressed", MakeStyle(background.Darkened(0.16f), border.Darkened(0.12f), 3));
    }

    private static StyleBoxFlat MakeStyle(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 12,
        CornerRadiusTopRight = 12,
        CornerRadiusBottomLeft = 12,
        CornerRadiusBottomRight = 12,
        ShadowColor = new Color(0f, 0f, 0f, 0.55f),
        ShadowSize = 5,
        ShadowOffset = new Vector2(0f, 3f),
        ContentMarginLeft = 10f,
        ContentMarginRight = 10f,
        ContentMarginTop = 5f,
        ContentMarginBottom = 5f,
    };

    private static void RemoveLegacyOddsArtifacts(NCard cardNode)
    {
        HideAndFree(cardNode.GetNodeOrNull<Button>(LegacyDisableButtonName));
        HideAndFree(cardNode.GetNodeOrNull<ColorRect>(LegacyDisabledOverlayName));
        try
        {
            HideAndFree(cardNode.Body.GetNodeOrNull<ColorRect>(LegacyDisabledOverlayName));
            cardNode.Body.Modulate = Colors.White;
        }
        catch { }
    }

    private static void RemoveUpgradeButton(NCard cardNode) => HideAndFree(cardNode.GetNodeOrNull<Button>(UpgradeButtonName));

    private static void RemoveUi(NCard cardNode)
    {
        RemoveUpgradeButton(cardNode);
        RemoveLegacyOddsArtifacts(cardNode);
        try { cardNode.RemoveMeta(UiTouchedMeta); } catch { }
    }

    private static void HideAndFree(CanvasItem? item)
    {
        if (item is null || !GodotObject.IsInstanceValid(item))
            return;
        try { item.Visible = false; } catch { }
        try { if (item is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore; } catch { }
        try { item.QueueFree(); } catch { }
    }

    private static void CleanupGrid(NCardLibraryGrid grid)
    {
        try
        {
            if (GridRowsField?.GetValue(grid) is not IEnumerable<List<NGridCardHolder>> rows)
                return;
            foreach (var holder in rows.SelectMany(row => row))
                if (holder?.CardNode is { } card)
                    RemoveUi(card);
        }
        catch { }
    }

    internal static void CleanupAllTouchedCards()
    {
        try
        {
            if (Engine.GetMainLoop() is SceneTree tree && tree.Root is { } root)
                CleanupRecursive(root, outsideOnly: false);
        }
        catch { }
    }

    internal static void CleanupTouchedCardsOutsideLibrary()
    {
        try
        {
            if (Engine.GetMainLoop() is SceneTree tree && tree.Root is { } root)
                CleanupRecursive(root, outsideOnly: true);
        }
        catch { }
    }

    private static void CleanupRecursive(Node node, bool outsideOnly)
    {
        try
        {
            if (node is NCard card && (!outsideOnly || !IsCardLibraryContext(card)))
                RemoveUi(card);
            foreach (var child in node.GetChildren())
                CleanupRecursive(child, outsideOnly);
        }
        catch { }
    }

    internal static bool IsCardLibraryContext(Node node)
    {
        try
        {
            var inspect = NGame.Instance?.InspectCardScreen;
            if (inspect is CanvasItem inspectItem && inspectItem.IsInsideTree() && inspectItem.Visible && inspectItem.IsVisibleInTree())
                return false;
        }
        catch { }

        NCardLibrary? library = null;
        NCardLibraryGrid? grid = null;
        for (var current = node; current != null; current = current.GetParent())
        {
            var typeName = current.GetType().FullName ?? current.GetType().Name;
            var nodeName = current.Name.ToString();
            if (NonLibraryContextNeedles.Any(value => typeName.Contains(value, StringComparison.OrdinalIgnoreCase) || nodeName.Contains(value, StringComparison.OrdinalIgnoreCase)))
                return false;
            grid ??= current as NCardLibraryGrid;
            if (current is NCardLibrary candidate)
            {
                library = candidate;
                break;
            }
        }

        if (library is null || grid is null || !IsVisible(library) || !IsVisible(grid) || !IsUnderCurrentScene(library))
            return false;
        return ReferenceEquals(GetLibraryGrid(library), grid);
    }

    private static bool IsVisible(Node node)
    {
        try { return node is CanvasItem item && item.IsInsideTree() && item.Visible && item.IsVisibleInTree(); }
        catch { return false; }
    }

    internal static bool IsUnderCurrentScene(Node node)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.CurrentScene is null)
                return false;
            for (var current = node; current != null; current = current.GetParent())
                if (ReferenceEquals(current, tree.CurrentScene))
                    return true;
        }
        catch { }
        return false;
    }

    internal static NCardLibraryGrid? GetLibraryGrid(NCardLibrary library)
    {
        try { return LibraryGridField?.GetValue(library) as NCardLibraryGrid; }
        catch { return null; }
    }

    internal static void ScheduleLibraryFilterRefresh(NCardLibrary library)
    {
        try
        {
            var timer = library.GetTree()?.CreateTimer(0.42);
            if (timer is null)
                return;
            timer.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(library) && GetLibraryGrid(library) is { } grid)
                    ApplyLibraryGrid(grid);
            };
        }
        catch { }
    }

    internal static void ScheduleFinalGridRefresh(NCardLibraryGrid grid)
    {
        if (GodotObject.IsInstanceValid(grid))
            ApplyLibraryGrid(grid);
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(grid))
                ApplyLibraryGrid(grid);
        }).CallDeferred();
    }

    private static string SafeId(CardModel card)
    {
        try { return card.Id.ToString(); }
        catch { return card.GetType().Name; }
    }
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuOpened))]
internal static class BdCardUpgradeLibraryOpenedPatch
{
    private static void Postfix(NCardLibrary __instance)
    {
        BdCardUpgradeStatsHud.SyncLibraryVisibility(__instance);
        if (BdCardUpgradeUi.GetLibraryGrid(__instance) is { } grid)
            BdCardUpgradeUi.ApplyLibraryGrid(grid);
    }
}

[HarmonyPatch(typeof(NCardLibrary), "UpdateFilter")]
internal static class BdCardUpgradeLibraryFilterPatch
{
    private static void Postfix(NCardLibrary __instance) => BdCardUpgradeUi.ScheduleLibraryFilterRefresh(__instance);
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuClosed))]
internal static class BdCardUpgradeLibraryClosedPatch
{
    private static void Postfix()
    {
        BdCardUpgradeUi.CleanupAllTouchedCards();
        BdCardUpgradeStatsHud.Hide();
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NSubmenu), "OnScreenVisibilityChange")]
internal static class BdCardUpgradeLibraryVisibilityPatch
{
    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NSubmenu __instance)
    {
        if (__instance is NCardLibrary library)
            BdCardUpgradeStatsHud.SyncLibraryVisibility(library);
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "InitGrid")]
internal static class BdCardUpgradeLibraryGridInitPatch
{
    private static void Postfix(NCardLibraryGrid __instance) => BdCardUpgradeUi.ApplyLibraryGrid(__instance);
}

[HarmonyPatch(typeof(NCardLibraryGrid), "FilterCards", [typeof(Func<CardModel, bool>), typeof(List<SortingOrders>)])]
internal static class BdCardUpgradeLibraryFinalFilterPatch
{
    private static void Postfix(NCardLibraryGrid __instance) => BdCardUpgradeUi.ScheduleFinalGridRefresh(__instance);
}

[HarmonyPatch(typeof(NCardLibraryGrid), "AssignCardsToRow")]
internal static class BdCardUpgradeLibraryGridAssignPatch
{
    private static void Postfix(NCardLibraryGrid __instance, List<NGridCardHolder> row)
    {
        var holders = row.ToArray();
        BdCardUpgradeUi.ApplyLibraryRowForGrid(__instance, holders);
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(__instance))
                BdCardUpgradeUi.ApplyLibraryRowForGrid(__instance, holders);
        }).CallDeferred();
    }
}

[HarmonyPatch(typeof(NGridCardHolder), nameof(NGridCardHolder.SetIsPreviewingUpgrade))]
internal static class BdCardUpgradeLibraryUpgradePreviewPatch
{
    private static void Postfix(NGridCardHolder __instance) => BdCardUpgradeUi.ReapplyAfterUpgradePreviewRefresh(__instance);
}

[HarmonyPatch(typeof(NCard), "set_Model")]
internal static class BdCardUpgradeModelSetPatch
{
    private static void Postfix(NCard __instance) => BdCardUpgradeUi.EnforceCardUiScope(__instance);
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class BdCardUpgradeReloadPatch
{
    private static void Postfix(NCard __instance) => BdCardUpgradeUi.EnforceCardUiScope(__instance);
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class BdCardUpgradeUpdateVisualsScopePatch
{
    private static void Postfix(NCard __instance) => BdCardUpgradeUi.EnforceCardUiScope(__instance);
}

[HarmonyPatch(typeof(NCard), nameof(NCard._ExitTree))]
internal static class BdCardUpgradeExitTreeScopePatch
{
    private static void Prefix(NCard __instance) => BdCardUpgradeUi.EnforceCardUiScope(__instance);
}
