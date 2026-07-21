using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using System.Reflection;

namespace BetterDefect;

/// <summary>
/// Small in-card UI for the dynamic reward odds system.
/// The reward weighting code stayed in DynamicOdds.cs, but the visible card
/// annotation / disable toggle was lost during the later card-pool rewrite.
/// This patch puts those controls back without BaseLib.
/// </summary>
internal static class BdDynamicOddsCardUi
{
    private const string ToggleButtonName = "BetterDefectDynamicOddsDisableButton";
    private const string UpgradeButtonName = "BetterDefectCardVersionUpgradeButton";
    private const string DisabledOverlayName = "BetterDefectDynamicOddsDisabledOverlay";
    private const string UiTouchedMeta = "better_defect_dynamic_odds_ui_touched";
    private const string ButtonStyledMeta = "better_defect_dynamic_odds_button_styled";
    private static readonly Vector2 ToggleButtonSize = new(176f, 56f);
    private static readonly string[] NonLibraryContextNeedles =
    [
        "NCombat", "CombatRoom", "Reward", "Merchant", "Shop",
        "NCardPileScreen", "CardPileScreen", "DeckScreen", "DeckView",
        "DrawPile", "DiscardPile", "ExhaustPile", "MasterDeck"
    ];
    private static readonly string[] CombatOrRewardNeedles = ["NCombat", "CombatRoom", "Reward", "Merchant", "Shop"];
    private static readonly object LabelReflectionLock = new();
    private static readonly Dictionary<Type, PropertyInfo?> LabelTextPropertyByType = new();
    private static readonly Dictionary<Type, MethodInfo?> LabelSetTextAutoSizeByType = new();
    private static readonly FieldInfo? GridHolderBaseCardField = AccessTools.Field(typeof(NGridCardHolder), "_baseCard");
    private static readonly MethodInfo? GridHolderUpdateCardModelMethod = AccessTools.Method(typeof(NGridCardHolder), "UpdateCardModel");
    private static FieldInfo? _cardRowsField;
    private static FieldInfo? _libraryGridField;
    private static FieldInfo? _descriptionLabelField;
    private static bool _cardRowsFieldResolved;
    private static bool _libraryGridFieldResolved;
    private static bool _descriptionLabelFieldResolved;
    private static string? _lastToggleKey;
    private static ulong _lastToggleMsec;
    private static string? _lastUpgradeToggleKey;
    private static ulong _lastUpgradeToggleMsec;
    private static ulong _lastScopeScanMsec;
    private static ulong _lastScopeGridInstanceId;
    private static bool _lastScopeHasOutsideCard;

    internal static void ApplyLibraryGrid(NCardLibraryGrid grid)
    {
        try
        {
            if (!IsCardLibraryContext(grid))
            {
                CleanupGridIfTouched(grid);
                BdDynamicOddsStatsHud.Hide();
                return;
            }

            var rows = GetCardRowsField()?.GetValue(grid) as IEnumerable<List<NGridCardHolder>>;
            if (rows != null)
            {
                foreach (var row in rows)
                    ApplyLibraryRow(row, assumeVerifiedLibrary: true);
            }
            BdDynamicOddsStatsHud.ShowForLibrary(grid);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] dynamic odds library grid UI skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void ApplyLibraryRow(IEnumerable<NGridCardHolder>? row, bool assumeVerifiedLibrary = false)
    {
        if (row == null) return;

        var appliedAny = false;
        NCard? statsContext = null;
        foreach (var holder in row)
        {
            try
            {
                var cardNode = holder?.CardNode;
                if (cardNode == null)
                    continue;

                if (holder is CanvasItem item && !item.Visible)
                {
                    RemoveBetterDefectUi(cardNode);
                    continue;
                }

                var inLibrary = assumeVerifiedLibrary || IsCardLibraryContext(cardNode);
                if (!inLibrary)
                {
                    RemoveBetterDefectUiIfTouched(cardNode);
                    continue;
                }

                ApplyLibraryCardUi(cardNode, assumeLibrary: true);
                appliedAny = true;
                statsContext ??= cardNode;
            }
            catch { }
        }

        if (appliedAny && statsContext is not null)
            BdDynamicOddsStatsHud.ShowForLibrary(statsContext);
    }

    internal static void ApplyLibraryRowForGrid(
        NCardLibraryGrid grid,
        IEnumerable<NGridCardHolder>? row)
    {
        if (row == null)
            return;

        // NCardLibraryGrid is also reused by the in-run master-deck screen.
        // The old AssignCardsToRow hook trusted the concrete grid type and
        // therefore injected encyclopedia-only controls into the run.  The
        // card nodes themselves may still be between parents while a row is
        // assigned, but the grid is already attached to its real owner, so use
        // the grid ancestry as the authoritative scope check.
        if (!IsCardLibraryContext(grid))
        {
            CleanupLibraryRow(row);
            BdDynamicOddsStatsHud.HideIfOutsideLibrary(grid);
            return;
        }

        ApplyLibraryRow(row, assumeVerifiedLibrary: true);
    }

    internal static void CleanupIfTouchedOutsideLibrary(NCard cardNode)
    {
        try
        {
            if (!HasBetterDefectUiTouched(cardNode))
                return;

            if (!IsCardLibraryContext(cardNode))
                RemoveBetterDefectUi(cardNode);
        }
        catch { }
    }

    internal static void CleanupTouchedCardsOutsideLibrary()
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
                return;

            CleanupTouchedCardsOutsideLibrary(tree.Root);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] dynamic odds tree cleanup skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void CleanupAllTouchedCards()
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
                return;

            CleanupAllTouchedCards(tree.Root);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] full card UI cleanup skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void ApplyLibraryCardUi(NCard cardNode, bool assumeLibrary = false)
    {
        try
        {
            if (!assumeLibrary && !IsCardLibraryContext(cardNode))
            {
                RemoveBetterDefectUiIfTouched(cardNode);
                return;
            }

            var card = cardNode.Model;
            if (card == null || !BdDynamicOdds.ShouldShowWeight(card))
            {
                RemoveBetterDefectUiIfTouched(cardNode);
                return;
            }

            ApplyDisabledCardLook(cardNode, card);
            AppendDynamicOddsLine(cardNode, card);
            EnsureDisableToggle(cardNode, card, assumeLibrary);
            EnsureVersionUpgradeToggle(cardNode, card, assumeLibrary);
            BdDynamicOddsStatsHud.ShowForLibrary(cardNode);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] dynamic odds card UI skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void EnforceCardUiScope(NCard cardNode)
    {
        try
        {
            if (IsCardLibraryContext(cardNode))
                return;

            // Card nodes are pooled by the game.  If an encyclopedia card that
            // had BetterDefect controls is reused for combat/shop/deck display,
            // the button and grey overlay can otherwise ride along with the
            // pooled node.  Strip only cards we touched or that still carry one
            // of our concrete child nodes so normal combat UpdateVisuals stays
            // cheap on Android.
            if (HasBetterDefectUiArtifact(cardNode))
                RemoveBetterDefectUi(cardNode);

            if (IsNonLibraryContext(cardNode))
                BdDynamicOddsStatsHud.HideIfOutsideLibrary(cardNode);
        }
        catch { }
    }

    private static void AppendDynamicOddsLine(NCard cardNode, CardModel card)
    {
        var label = GetDescriptionLabel(cardNode);
        if (label == null)
            return;

        var current = GetLabelText(label);
        if (string.IsNullOrWhiteSpace(current))
            return;

        var cleaned = RemoveExistingDynamicLine(current);
        var line = BdDynamicOdds.GetDisplayLine(card);
        var insert = $"\n[font_size=22]{line}[/font_size]";
        var updated = cleaned.Contains("[/center]", StringComparison.Ordinal)
            ? cleaned.Replace("[/center]", insert + "[/center]", StringComparison.Ordinal)
            : cleaned + insert;

        if (string.Equals(updated, current, StringComparison.Ordinal))
            return;

        MarkBetterDefectUiTouched(cardNode);
        SetLabelTextAutoSize(label, updated);
    }

    private static string RemoveExistingDynamicLine(string text)
    {
        // Clean both the correct line and broken lines left by earlier mojibake builds.
        var markers = new[] { "动态出率：", "?????", "åŠ¨æ€å‡ºçŽ‡" };
        foreach (var marker in markers)
        {
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                continue;

            var lineStart = text.LastIndexOf('\n', index);
            if (lineStart < 0)
                lineStart = Math.Max(0, text.LastIndexOf("[font_size=22]", index, StringComparison.Ordinal));
            if (lineStart < 0)
                lineStart = index;

            var lineEnd = text.IndexOf("[/font_size]", index, StringComparison.Ordinal);
            if (lineEnd >= 0)
                lineEnd += "[/font_size]".Length;
            else
            {
                lineEnd = text.IndexOf('\n', index);
                if (lineEnd < 0) lineEnd = text.Length;
            }

            return RemoveExistingDynamicLine(text.Remove(lineStart, Math.Max(0, lineEnd - lineStart)));
        }

        return text;
    }

    private static void EnsureDisableToggle(NCard cardNode, CardModel card, bool assumeLibrary = false)
    {
        var existing = cardNode.GetNodeOrNull<Button>(ToggleButtonName);
        var shouldShow = assumeLibrary || ShouldShowTouchToggle(cardNode);
        if (!shouldShow)
        {
            existing?.QueueFree();
            return;
        }

        var disabled = BdDynamicOdds.IsCardDisabled(card);
        var button = existing;
        if (button == null)
        {
            MarkBetterDefectUiTouched(cardNode);
            button = new Button
            {
                Name = ToggleButtonName,
                Size = ToggleButtonSize,
                CustomMinimumSize = ToggleButtonSize,
                Position = GetTogglePosition(ToggleButtonSize),
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 4095,
                TooltipText = "BetterDefect：点击禁用/启用这张机器人卡牌的奖励出率"
            };
            ApplyCardBeautifyStyle(button);
            void ToggleFromButton()
            {
                ToggleCardFromButton(cardNode);
            }

            button.ButtonDown += ToggleFromButton;
            button.Pressed += ToggleFromButton;
            button.GuiInput += ev =>
            {
                if (ev is InputEventScreenTouch touch && touch.Pressed)
                {
                    button.AcceptEvent();
                    ToggleCardFromButton(cardNode);
                }
                else if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
                {
                    button.AcceptEvent();
                    ToggleCardFromButton(cardNode);
                }
            };
            cardNode.AddChild(button);
        }

        button.Size = ToggleButtonSize;
        button.CustomMinimumSize = ToggleButtonSize;
        ApplyCardBeautifyStyle(button);
        button.Text = disabled ? "启用出率" : "禁用出率";
        button.Modulate = Colors.White;
        button.Position = GetTogglePosition(button.Size);
        button.Visible = true;
    }

    private static void ToggleCardFromButton(NCard cardNode)
    {
        try
        {
            var key = SafeId(cardNode.Model);
            var now = Time.GetTicksMsec();
            if (string.Equals(_lastToggleKey, key, StringComparison.Ordinal) && now - _lastToggleMsec < 350UL)
                return;
            _lastToggleKey = key;
            _lastToggleMsec = now;

                if (BdDynamicOdds.ToggleCardDisabled(cardNode.Model))
                {
                    MainFile.Logger.Info($"[BetterDefect] dynamic odds toggle clicked on {SafeId(cardNode.Model)}.");
                    if (cardNode.Model != null)
                        ApplyLibraryCardUi(cardNode);
                    BdDynamicOddsStatsHud.RefreshFrom(cardNode);
                }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] dynamic odds toggle failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void EnsureVersionUpgradeToggle(NCard cardNode, CardModel card, bool assumeLibrary = false)
    {
        var existing = cardNode.GetNodeOrNull<Button>(UpgradeButtonName);
        var shouldShow = (assumeLibrary || ShouldShowTouchToggle(cardNode)) && BdCardVersionUpgrades.IsEligible(card);
        if (!shouldShow)
        {
            existing?.QueueFree();
            return;
        }

        var enabled = BdDynamicOdds.IsCardVersionUpgraded(card);
        var button = existing;
        if (button == null)
        {
            MarkBetterDefectUiTouched(cardNode);
            button = new Button
            {
                Name = UpgradeButtonName,
                Size = ToggleButtonSize,
                CustomMinimumSize = ToggleButtonSize,
                Position = GetUpgradeTogglePosition(),
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 4096
            };

            void ToggleFromButton() => ToggleVersionUpgradeFromButton(cardNode);
            button.ButtonDown += ToggleFromButton;
            button.Pressed += ToggleFromButton;
            button.GuiInput += ev =>
            {
                if (ev is InputEventScreenTouch touch && touch.Pressed)
                {
                    button.AcceptEvent();
                    ToggleVersionUpgradeFromButton(cardNode);
                }
                else if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
                {
                    button.AcceptEvent();
                    ToggleVersionUpgradeFromButton(cardNode);
                }
            };
            cardNode.AddChild(button);
        }

        button.Size = ToggleButtonSize;
        button.CustomMinimumSize = ToggleButtonSize;
        button.Position = GetUpgradeTogglePosition();
        var usedPoints = BdDynamicOdds.GetUsedCardPointCount();
        var budgetFull = !enabled && usedPoints >= BdDynamicOdds.MaxCardPointBudget;
        var targetLabel = BdCardVersionUpgrades.GetTargetVersionLabel(card);
        button.Disabled = budgetFull;
        button.Text = enabled
            ? targetLabel.StartsWith("改造：", StringComparison.Ordinal) ? targetLabel : $"已改：{targetLabel}"
            : budgetFull ? "改造：点数已满" : "改造：关闭";
        button.TooltipText = budgetFull
            ? $"BetterDefect：改造点数已满（{usedPoints}/{BdDynamicOdds.MaxCardPointBudget}），先重新启用一张已禁用卡或关闭一个历史改造。"
            : $"BetterDefect：消耗1点改造点数，切换到 {targetLabel}。效果：{BdCardVersionUpgrades.GetTargetEffectSummary(card)}。与禁用卡牌共享35点上限。";
        ApplyVersionUpgradeStyle(button, enabled);
        button.Visible = true;
    }

    private static void ToggleVersionUpgradeFromButton(NCard cardNode)
    {
        try
        {
            var key = SafeId(cardNode.Model);
            var now = Time.GetTicksMsec();
            if (string.Equals(_lastUpgradeToggleKey, key, StringComparison.Ordinal) && now - _lastUpgradeToggleMsec < 350UL)
                return;
            _lastUpgradeToggleKey = key;
            _lastUpgradeToggleMsec = now;

            if (!BdDynamicOdds.ToggleCardVersionUpgrade(cardNode.Model))
            {
                BdDynamicOddsStatsHud.RefreshFrom(cardNode);
                return;
            }

            if (cardNode.Model != null)
            {
                BdCardVersionUpgrades.RefreshCanonicalFor(cardNode.Model);
                RefreshLibraryCardsOfType(cardNode);
                // NGridCardHolder caches both a base model and an upgraded
                // clone. Rebuilding those models does not repaint the base
                // card when the global "view upgrades" tickbox is currently
                // off, so the button could turn green while the visible
                // number still looked unchanged. Repaint the clicked card at
                // the end of this frame, after the button input has finished.
                ScheduleLibraryCardRefresh(cardNode);
            }
            BdDynamicOddsStatsHud.RefreshFrom(cardNode);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] card version upgrade toggle failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RefreshLibraryCardsOfType(NCard sourceCard)
    {
        var model = sourceCard.Model;
        if (model == null) return;

        var wantedType = model.GetType();
        RebuildGridHolderUpgradeCache(sourceCard, wantedType);
        var root = sourceCard.GetTree()?.CurrentScene ?? sourceCard;
        RefreshLibraryCardsOfTypeRecursive(root, wantedType);
    }

    private static void ScheduleLibraryCardRefresh(NCard cardNode)
    {
        void Refresh()
        {
            try
            {
                if (!GodotObject.IsInstanceValid(cardNode) || cardNode.Model == null)
                    return;
                BdCardVersionUpgrades.ApplyToModel(cardNode.Model);
                if (cardNode.IsNodeReady())
                    cardNode.UpdateVisuals(cardNode.DisplayingPile, CardPreviewMode.Normal);
                ApplyLibraryCardUi(cardNode);
            }
            catch { }
        }

        Callable.From(() =>
        {
            Refresh();
            Callable.From(Refresh).CallDeferred();
        }).CallDeferred();
    }

    internal static void ReapplyAfterUpgradePreviewRefresh(NGridCardHolder holder)
    {
        try
        {
            var cardNode = holder.CardNode;
            if (cardNode == null)
                return;

            // SetIsPreviewingUpgrade is defined on the generic NGridCardHolder
            // and is also called by the in-run deck view. The old hook trusted
            // the holder type and re-injected encyclopedia controls after the
            // row-scope cleanup had already removed them. Re-check the actual
            // ancestry on every deferred pass instead.
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(holder) || holder.CardNode == null)
                    return;
                ApplyLibraryCardUi(holder.CardNode);
                Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(holder) && holder.CardNode != null)
                        ApplyLibraryCardUi(holder.CardNode);
                }).CallDeferred();
            }).CallDeferred();
        }
        catch { }
    }

    private static void RebuildGridHolderUpgradeCache(NCard sourceCard, Type wantedType)
    {
        try
        {
            NGridCardHolder? holder = null;
            for (var parent = sourceCard.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent is NGridCardHolder gridHolder)
                {
                    holder = gridHolder;
                    break;
                }
            }

            if (holder == null || GridHolderUpdateCardModelMethod == null)
                return;

            // NGridCardHolder creates and caches a private upgraded clone when
            // the row is assigned.  Repaint-only refreshes cannot change that
            // clone, so switching "查看升级" after our button otherwise shows
            // the previous historical version until the library is reopened.
            var baseCard = GridHolderBaseCardField?.GetValue(holder) as CardModel;
            if (baseCard == null || baseCard.GetType() != wantedType)
                return;

            BdCardVersionUpgrades.ApplyToModel(baseCard);
            if (!ReferenceEquals(sourceCard.Model, baseCard))
                sourceCard.Model = baseCard;
            GridHolderUpdateCardModelMethod.Invoke(holder, null);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] failed to rebuild library upgrade cache: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RefreshLibraryCardsOfTypeRecursive(Node root, Type wantedType)
    {
        try
        {
            if (root is NCard cardNode && cardNode.Model?.GetType() == wantedType)
            {
                // The library keeps separate normal and "+" card nodes.  Both
                // can remain pooled after the preview checkbox changes, so
                // refresh every copy of this card type instead of only the
                // button's current node.  This prevents a stale old-version
                // value from reappearing when "查看升级" is toggled.
                BdCardVersionUpgrades.ApplyToModel(cardNode.Model);
                if (cardNode.IsNodeReady())
                    cardNode.UpdateVisuals(cardNode.DisplayingPile, CardPreviewMode.Normal);
                ApplyLibraryCardUi(cardNode);
            }

            foreach (var child in root.GetChildren())
            {
                if (child is Node childNode)
                    RefreshLibraryCardsOfTypeRecursive(childNode, wantedType);
            }
        }
        catch { }
    }

    private static void ApplyDisabledCardLook(NCard cardNode, CardModel card)
    {
        var disabled = BdDynamicOdds.IsCardDisabled(card);

        try
        {
            // Put the mask on the card Body (%CardContainer), not on the NCard root.
            // The root rect changes between grid cards and the mobile inspect popup;
            // Body is the visible card container, so the overlay stays aligned.
            var rootOverlay = cardNode.GetNodeOrNull<ColorRect>(DisabledOverlayName);
            rootOverlay?.QueueFree();

            var body = cardNode.Body;
            var overlay = body.GetNodeOrNull<ColorRect>(DisabledOverlayName);
            if (disabled)
            {
                MarkBetterDefectUiTouched(cardNode);
                if (overlay == null)
                {
                    overlay = new ColorRect
                    {
                        Name = DisabledOverlayName,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                        ZIndex = 3200,
                        Color = new Color(0.035f, 0.035f, 0.04f, 0.42f)
                    };
                    body.AddChild(overlay);
                }

                overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                overlay.OffsetLeft = 0;
                overlay.OffsetTop = 0;
                overlay.OffsetRight = 0;
                overlay.OffsetBottom = 0;
                overlay.Visible = true;
                body.Modulate = new Color(0.68f, 0.68f, 0.68f, 0.98f);
            }
            else
            {
                overlay?.QueueFree();
                body.Modulate = Colors.White;
            }
        }
        catch
        {
            cardNode.Modulate = disabled
                ? new Color(0.62f, 0.62f, 0.62f, 1f)
                : Colors.White;
        }
    }

    private static void RemoveBetterDefectUi(NCard cardNode)
    {
        // Hide artifacts synchronously before QueueFree. Encyclopedia cards are
        // pooled and can be reparented to another screen in the same frame.
        HideAndQueueFree(cardNode.GetNodeOrNull<Button>(ToggleButtonName));
        HideAndQueueFree(cardNode.GetNodeOrNull<Button>(UpgradeButtonName));
        HideAndQueueFree(cardNode.GetNodeOrNull<ColorRect>(DisabledOverlayName));
        try { HideAndQueueFree(cardNode.Body.GetNodeOrNull<ColorRect>(DisabledOverlayName)); } catch { }
        try { cardNode.Body.Modulate = Colors.White; } catch { }
        try { cardNode.Modulate = Colors.White; } catch { }
        try
        {
            var label = GetDescriptionLabel(cardNode);
            var current = label != null ? GetLabelText(label) : null;
            if (!string.IsNullOrEmpty(current))
            {
                var cleaned = RemoveExistingDynamicLine(current);
                if (!string.Equals(cleaned, current, StringComparison.Ordinal))
                    SetLabelTextAutoSize(label!, cleaned);
            }
        }
        catch { }
        try { cardNode.RemoveMeta(UiTouchedMeta); } catch { }
    }

    private static void HideAndQueueFree(CanvasItem? item)
    {
        if (item is null || !GodotObject.IsInstanceValid(item))
            return;

        try { item.Visible = false; } catch { }
        try
        {
            if (item is Control control)
                control.MouseFilter = Control.MouseFilterEnum.Ignore;
        }
        catch { }
        try { item.QueueFree(); } catch { }
    }

    private static void RemoveBetterDefectUiIfTouched(NCard cardNode)
    {
        if (!HasBetterDefectUiTouched(cardNode))
            return;

        RemoveBetterDefectUi(cardNode);
    }

    private static bool HasBetterDefectUiArtifact(NCard cardNode)
    {
        try
        {
            if (HasBetterDefectUiTouched(cardNode))
                return true;
        }
        catch { }

        try
        {
            if (cardNode.GetNodeOrNull<Button>(ToggleButtonName) != null)
                return true;
        }
        catch { }

        try
        {
            if (cardNode.GetNodeOrNull<Button>(UpgradeButtonName) != null)
                return true;
        }
        catch { }

        try
        {
            if (cardNode.GetNodeOrNull<ColorRect>(DisabledOverlayName) != null)
                return true;
        }
        catch { }

        try
        {
            if (cardNode.Body.GetNodeOrNull<ColorRect>(DisabledOverlayName) != null)
                return true;
        }
        catch { }

        return false;
    }

    private static bool HasBetterDefectUiTouched(NCard cardNode)
    {
        try { return cardNode.HasMeta(UiTouchedMeta); }
        catch { return false; }
    }

    private static void CleanupTouchedCardsOutsideLibrary(Node root)
    {
        try
        {
            if (root is NCard cardNode)
                CleanupIfTouchedOutsideLibrary(cardNode);

            foreach (var child in root.GetChildren())
            {
                if (child is Node childNode)
                    CleanupTouchedCardsOutsideLibrary(childNode);
            }
        }
        catch { }
    }

    private static void CleanupAllTouchedCards(Node root)
    {
        try
        {
            if (root is NCard cardNode && HasBetterDefectUiArtifact(cardNode))
                RemoveBetterDefectUi(cardNode);

            foreach (var child in root.GetChildren())
            {
                if (child is Node childNode)
                    CleanupAllTouchedCards(childNode);
            }
        }
        catch { }
    }

    private static void CleanupGridIfTouched(NCardLibraryGrid grid)
    {
        try
        {
            var rows = GetCardRowsField()?.GetValue(grid) as IEnumerable<List<NGridCardHolder>>;
            if (rows == null)
                return;

            foreach (var row in rows)
            {
                foreach (var holder in row)
                {
                    try
                    {
                        var cardNode = holder?.CardNode;
                        if (cardNode != null)
                            RemoveBetterDefectUiIfTouched(cardNode);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static void CleanupLibraryRow(IEnumerable<NGridCardHolder> row)
    {
        foreach (var holder in row)
        {
            try
            {
                var cardNode = holder?.CardNode;
                if (cardNode != null && HasBetterDefectUiArtifact(cardNode))
                    RemoveBetterDefectUi(cardNode);
            }
            catch { }
        }
    }

    private static void MarkBetterDefectUiTouched(NCard cardNode)
    {
        try { cardNode.SetMeta(UiTouchedMeta, Variant.From(true)); } catch { }
    }

    private static Vector2 GetTogglePosition(Vector2 size)
    {
        // Keep the touch button inside the card bounds.  Putting it under the
        // card (Y > defaultSize.Y) is clipped by several mobile holders, which
        // made the button disappear.
        return new Vector2(
            28f,
            48f);
    }

    private static Vector2 GetUpgradeTogglePosition()
    {
        // CardBeautify's card-face selector is at (28, -14).  Keep the same
        // 176x56 touch target and place this one immediately above it.
        return new Vector2(28f, -76f);
    }

    private static void ApplyCardBeautifyStyle(Button button)
    {
        try
        {
            if (button.HasMeta(ButtonStyledMeta))
                return;
        }
        catch { }

        // Match CardBeautify's mobile Spire-style per-card art switch button.
        button.CustomMinimumSize = ToggleButtonSize;
        button.Scale = Vector2.One;
        button.ClipText = true;
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", new Color(1.0f, 0.91f, 0.58f));
        button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.98f, 0.75f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.88f, 0.73f, 0.38f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.45f, 0.32f));
        button.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color(0.16f, 0.105f, 0.065f, 0.92f), new Color(0.78f, 0.58f, 0.28f, 1f), 3));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle(new Color(0.22f, 0.145f, 0.075f, 0.96f), new Color(1.0f, 0.78f, 0.36f, 1f), 4));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(new Color(0.105f, 0.07f, 0.045f, 0.98f), new Color(0.66f, 0.46f, 0.22f, 1f), 3));
        button.AddThemeStyleboxOverride("focus", MakeButtonStyle(new Color(0.22f, 0.145f, 0.075f, 0.36f), new Color(1.0f, 0.84f, 0.42f, 1f), 5));
        try { button.SetMeta(ButtonStyledMeta, Variant.From(true)); } catch { }
    }

    private static void ApplyVersionUpgradeStyle(Button button, bool enabled)
    {
        button.CustomMinimumSize = ToggleButtonSize;
        button.Scale = Vector2.One;
        button.ClipText = true;
        button.Modulate = Colors.White;
        button.AddThemeFontSizeOverride("font_size", 18);

        var font = enabled
            ? new Color(0.82f, 1f, 0.72f, 1f)
            : new Color(1f, 0.78f, 0.67f, 1f);
        var hoverFont = enabled
            ? new Color(0.94f, 1f, 0.86f, 1f)
            : new Color(1f, 0.90f, 0.82f, 1f);
        var bg = enabled
            ? new Color(0.09f, 0.31f, 0.12f, 0.95f)
            : new Color(0.38f, 0.08f, 0.055f, 0.95f);
        var border = enabled
            ? new Color(0.43f, 0.82f, 0.31f, 1f)
            : new Color(0.92f, 0.31f, 0.20f, 1f);

        button.AddThemeColorOverride("font_color", font);
        button.AddThemeColorOverride("font_hover_color", hoverFont);
        button.AddThemeColorOverride("font_pressed_color", font.Darkened(0.18f));
        button.AddThemeColorOverride("font_disabled_color", font.Darkened(0.35f));
        button.AddThemeStyleboxOverride("normal", MakeButtonStyle(bg, border, 3));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle(bg.Lightened(0.10f), border.Lightened(0.10f), 4));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(bg.Darkened(0.16f), border.Darkened(0.12f), 3));
        button.AddThemeStyleboxOverride("focus", MakeButtonStyle(bg, border.Lightened(0.16f), 5));
    }

    private static StyleBoxFlat MakeButtonStyle(Color bg, Color border, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
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
            ContentMarginBottom = 5f
        };
    }

    private static bool ShouldShowTouchToggle(NCard cardNode)
    {
        return IsCardLibraryContext(cardNode);
    }

    internal static bool IsCardLibraryContext(Node node)
    {
        NCardLibrary? library = null;
        NCardLibraryGrid? grid = null;

        for (var n = node; n != null; n = n.GetParent())
        {
            var typeName = n.GetType().FullName ?? n.GetType().Name;
            var nodeName = n.Name.ToString();

            // Keep the dynamic-odds controls strictly out of any in-run card
            // inspection screen.  NCardPileScreen/NCardGrid live near the
            // CardLibrary namespace, so the old string fallback accidentally
            // treated deck/draw/discard/exhaust/shop deck views as the real
            // encyclopedia and injected the disable button there.
            if (ContainsAny(typeName, NonLibraryContextNeedles) ||
                ContainsAny(nodeName, NonLibraryContextNeedles))
                return false;

            if (grid is null && n is NCardLibraryGrid candidateGrid)
                grid = candidateGrid;

            if (n is NCardLibrary candidateLibrary)
            {
                library = candidateLibrary;
                break;
            }
        }

        // A card-detail popup is also hosted below NCardLibrary. Require the
        // exact live grid owned by that library, not just a library ancestor.
        if (library is null || grid is null ||
            !IsVisibleInTreeStrict(library) || !IsVisibleInTreeStrict(grid))
            return false;

        // The main-menu scene may remain alive after the run is resumed. A
        // cached encyclopedia can therefore still report VisibleInTree while
        // combat is the active scene. Never treat such a detached old screen
        // as the current encyclopedia.
        if (!IsUnderCurrentScene(library))
            return false;

        var ownedGrid = GetLibraryGrid(library);
        return ownedGrid is not null &&
               ReferenceEquals(ownedGrid, grid);
    }

    private static bool HasVisibleCardOutsideGrid(NCardLibraryGrid grid)
    {
        try
        {
            var now = Time.GetTicksMsec();
            var gridId = grid.GetInstanceId();
            if (_lastScopeGridInstanceId == gridId && now - _lastScopeScanMsec < 100)
                return _lastScopeHasOutsideCard;

            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root is null)
                return false;

            _lastScopeHasOutsideCard = HasVisibleCardOutsideGrid(tree.Root, grid);
            _lastScopeGridInstanceId = gridId;
            _lastScopeScanMsec = now;
            return _lastScopeHasOutsideCard;
        }
        catch { return false; }
    }

    private static bool HasVisibleCardOutsideGrid(Node root, NCardLibraryGrid grid)
    {
        try
        {
            if (ReferenceEquals(root, grid))
                return false; // Skip the encyclopedia grid and all its cards.

            if (root is NCard card && IsVisibleInTreeStrict(card))
                return true;

            foreach (var child in root.GetChildren())
            {
                if (child is Node childNode && HasVisibleCardOutsideGrid(childNode, grid))
                    return true;
            }
        }
        catch { }

        return false;
    }

    private static bool IsNonLibraryContext(Node node)
    {
        for (var n = node; n != null; n = n.GetParent())
        {
            var typeName = n.GetType().FullName ?? n.GetType().Name;
            var nodeName = n.Name.ToString();
            if (ContainsAny(typeName, NonLibraryContextNeedles) ||
                ContainsAny(nodeName, NonLibraryContextNeedles))
                return true;
        }

        return false;
    }

    private static bool IsVisibleInTreeStrict(Node node)
    {
        try
        {
            if (node is CanvasItem item)
                return item.IsInsideTree() && item.Visible && item.IsVisibleInTree();
        }
        catch { }

        return false;
    }

    internal static bool IsUnderCurrentScene(Node node)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.CurrentScene is null)
                return false;

            for (var current = node; current != null; current = current.GetParent())
            {
                if (ReferenceEquals(current, tree.CurrentScene))
                    return true;
            }
        }
        catch { }

        return false;
    }

    private static bool IsCombatOrRewardContext(Node node)
    {
        for (var n = node; n != null; n = n.GetParent())
        {
            var typeName = n.GetType().FullName ?? n.GetType().Name;
            var nodeName = n.Name.ToString();
            if (ContainsAny(typeName, CombatOrRewardNeedles) ||
                ContainsAny(nodeName, CombatOrRewardNeedles))
                return true;
        }
        return false;
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsLargeVisibleCard(NCard cardNode)
    {
        try
        {
            var rect = cardNode.GetGlobalRect();
            if (rect.Size.X >= 230f && rect.Size.Y >= 320f)
                return true;
        }
        catch { }

        try
        {
            if (cardNode.Scale.X >= 0.85f && cardNode.Scale.Y >= 0.85f)
                return true;
        }
        catch { }

        return false;
    }

    private static string SafeId(CardModel? card)
    {
        if (card == null) return "<null>";
        try { return card.Id.ToString(); } catch { return card.GetType().Name; }
    }

    internal static NCardLibraryGrid? GetLibraryGrid(NCardLibrary library)
    {
        try { return GetLibraryGridField()?.GetValue(library) as NCardLibraryGrid; }
        catch { return null; }
    }

    internal static void ScheduleLibraryFilterRefresh(NCardLibrary library)
    {
        try
        {
            // Text search deliberately waits 250 ms before rebuilding the
            // grid. The AssignCardsToRow deferrals can therefore finish before
            // the final searched card is installed. Run one refresh after that
            // debounce/animation window and another on the following frame.
            var tree = library.GetTree();
            if (tree == null)
                return;
            var timer = tree.CreateTimer(0.42);
            timer.Timeout += () =>
            {
                try
                {
                    if (!GodotObject.IsInstanceValid(library))
                        return;
                    var grid = GetLibraryGrid(library);
                    if (grid == null)
                        return;
                    ApplyLibraryGrid(grid);
                    Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(grid))
                            ApplyLibraryGrid(grid);
                    }).CallDeferred();
                }
                catch { }
            };
        }
        catch { }
    }

    internal static void ScheduleFinalGridRefresh(NCardLibraryGrid grid)
    {
        try
        {
            // FilterCards is the final call after the search debounce. Its
            // SetCards work can still queue a last visual repaint, so restore
            // the controls once immediately and once 150 ms later.
            ApplyLibraryGrid(grid);
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(grid))
                    ApplyLibraryGrid(grid);
            }).CallDeferred();

            var tree = grid.GetTree();
            if (tree == null)
                return;
            void RefreshIfAlive()
            {
                try
                {
                    if (GodotObject.IsInstanceValid(grid))
                        ApplyLibraryGrid(grid);
                }
                catch { }
            }

            var timer = tree.CreateTimer(0.15);
            timer.Timeout += RefreshIfAlive;
            // Some card arts finish their holder/visual animation later than
            // the ordinary cards. A final late pass keeps those cards from
            // losing all three encyclopedia controls.
            var lateTimer = tree.CreateTimer(0.90);
            lateTimer.Timeout += RefreshIfAlive;
        }
        catch { }
    }

    private static object? GetDescriptionLabel(NCard cardNode)
    {
        try { return GetDescriptionLabelField()?.GetValue(cardNode); }
        catch { return null; }
    }

    private static FieldInfo? GetCardRowsField()
    {
        if (_cardRowsFieldResolved)
            return _cardRowsField;
        lock (LabelReflectionLock)
        {
            if (!_cardRowsFieldResolved)
            {
                _cardRowsField = AccessTools.Field(typeof(NCardGrid), "_cardRows");
                _cardRowsFieldResolved = true;
            }
            return _cardRowsField;
        }
    }

    private static FieldInfo? GetLibraryGridField()
    {
        if (_libraryGridFieldResolved)
            return _libraryGridField;
        lock (LabelReflectionLock)
        {
            if (!_libraryGridFieldResolved)
            {
                _libraryGridField = AccessTools.Field(typeof(NCardLibrary), "_grid");
                _libraryGridFieldResolved = true;
            }
            return _libraryGridField;
        }
    }

    private static FieldInfo? GetDescriptionLabelField()
    {
        if (_descriptionLabelFieldResolved)
            return _descriptionLabelField;
        lock (LabelReflectionLock)
        {
            if (!_descriptionLabelFieldResolved)
            {
                _descriptionLabelField = AccessTools.Field(typeof(NCard), "_descriptionLabel");
                _descriptionLabelFieldResolved = true;
            }
            return _descriptionLabelField;
        }
    }

    private static string? GetLabelText(object label)
    {
        try
        {
            var prop = GetLabelTextProperty(label.GetType());
            return prop?.GetValue(label) as string;
        }
        catch { return null; }
    }

    private static void SetLabelTextAutoSize(object label, string text)
    {
        try
        {
            var method = GetLabelSetTextAutoSizeMethod(label.GetType());
            method?.Invoke(label, [text]);
        }
        catch { }
    }

    private static PropertyInfo? GetLabelTextProperty(Type type)
    {
        lock (LabelReflectionLock)
        {
            if (LabelTextPropertyByType.TryGetValue(type, out var cached))
                return cached;
        }

        var prop = type.GetProperty("Text");
        lock (LabelReflectionLock)
        {
            LabelTextPropertyByType[type] = prop;
        }
        return prop;
    }

    private static MethodInfo? GetLabelSetTextAutoSizeMethod(Type type)
    {
        lock (LabelReflectionLock)
        {
            if (LabelSetTextAutoSizeByType.TryGetValue(type, out var cached))
                return cached;
        }

        var method = type.GetMethod("SetTextAutoSize", [typeof(string)]);
        lock (LabelReflectionLock)
        {
            LabelSetTextAutoSizeByType[type] = method;
        }
        return method;
    }
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuOpened))]
internal static class BdDynamicOddsCardLibraryOpenedPatch
{
    private static void Postfix(NCardLibrary __instance)
    {
        try
        {
            BdDynamicOddsStatsHud.SyncLibraryVisibility(__instance);
            var grid = BdDynamicOddsCardUi.GetLibraryGrid(__instance);
            if (grid != null)
                BdDynamicOddsCardUi.ApplyLibraryGrid(grid);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] dynamic odds library-open UI refresh skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCardLibrary), "UpdateFilter")]
internal static class BdDynamicOddsCardLibraryFilterPatch
{
    private static void Postfix(NCardLibrary __instance)
    {
        BdDynamicOddsCardUi.ScheduleLibraryFilterRefresh(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuClosed))]
internal static class BdDynamicOddsCardLibraryClosedPatch
{
    private static void Postfix(NCardLibrary __instance)
    {
        BdDynamicOddsCardUi.CleanupTouchedCardsOutsideLibrary();
        BdDynamicOddsStatsHud.Hide();
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NSubmenu), "OnScreenVisibilityChange")]
internal static class BdDynamicOddsCardLibraryVisibilityPatch
{
    // MonoMod's ARM64 detour for this virtual NSubmenu method terminates the
    // v0.103.2 Android process during Harmony.PatchAll. Mobile builds disable
    // it and rely on DisableStatsHud's 350 ms exact-library watcher instead.
    private const bool DisableUnsafeAndroidVisibilityDetour = false;
    private static bool Prepare() => !DisableUnsafeAndroidVisibilityDetour;

    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NSubmenu __instance)
    {
        // OnSubmenuOpened/Closed only fire for stack push/pop. Opening card
        // details hides the library and returning shows it again without either
        // callback, which caused both the leaked bar and the missing-bar bug.
        if (__instance is NCardLibrary library)
            BdDynamicOddsStatsHud.SyncLibraryVisibility(library);
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "InitGrid")]
internal static class BdDynamicOddsCardLibraryGridInitPatch
{
    private static void Postfix(NCardLibraryGrid __instance)
    {
        BdDynamicOddsCardUi.ApplyLibraryGrid(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "FilterCards",
    [typeof(Func<CardModel, bool>), typeof(List<SortingOrders>)])]
internal static class BdDynamicOddsCardLibraryFinalFilterPatch
{
    private static void Postfix(NCardLibraryGrid __instance)
    {
        BdDynamicOddsCardUi.ScheduleFinalGridRefresh(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "AssignCardsToRow")]
internal static class BdDynamicOddsCardLibraryGridAssignPatch
{
    private static void Postfix(NCardLibraryGrid __instance, List<NGridCardHolder> row)
    {
        // NCardLibraryGrid is shared by the encyclopedia and the in-run master
        // deck. Validate the owning grid on every pass; only the card nodes can
        // be temporarily unparented during assignment.
        var assignedHolders = row.ToArray();
        BdDynamicOddsCardUi.ApplyLibraryRowForGrid(__instance, assignedHolders);

        // NCard.UpdateVisuals can still run after AssignCardsToRow while the
        // holder is between parents and remove controls as an anti-leak guard.
        // Re-apply at the end of this frame and once more on the next frame,
        // when the row is guaranteed to be attached to the encyclopedia.
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(__instance))
                return;
            BdDynamicOddsCardUi.ApplyLibraryRowForGrid(__instance, assignedHolders);
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(__instance))
                    BdDynamicOddsCardUi.ApplyLibraryRowForGrid(__instance, assignedHolders);
            }).CallDeferred();
        }).CallDeferred();
    }
}

[HarmonyPatch(typeof(NGridCardHolder), nameof(NGridCardHolder.SetIsPreviewingUpgrade))]
internal static class BdDynamicOddsCardLibraryUpgradePreviewPatch
{
    private static void Postfix(NGridCardHolder __instance)
    {
        BdDynamicOddsCardUi.ReapplyAfterUpgradePreviewRefresh(__instance);
    }
}

[HarmonyPatch(typeof(NCard), "set_Model")]
internal static class BdDynamicOddsCardModelSetPatch
{
    // Harmony/MonoMod on the v103 Android ARM64 runtime can segfault while
    // detouring NCard.Model's setter. The encyclopedia already has concrete
    // NCardLibraryGrid refresh hooks, while the remaining Reload/UpdateVisuals
    // scope guards still remove leaked controls from reused card nodes.
    // Keep the setter hook on PC, but do not generate this unsafe detour on
    // Android.
    // The v103 source-preparation helper flips this compile-time constant to
    // true. This is more reliable than runtime OS detection because the
    // bundled Android .NET runtime reports an unknown platform.
    private const bool DisableUnsafeAndroidSetterDetour = false;
    private static bool Prepare() => !DisableUnsafeAndroidSetterDetour;

    private static void Postfix(NCard __instance)
    {
        BdDynamicOddsCardUi.EnforceCardUiScope(__instance);
    }
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class BdDynamicOddsCardReloadPatch
{
    private static void Postfix(NCard __instance)
    {
        BdDynamicOddsCardUi.EnforceCardUiScope(__instance);
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class BdDynamicOddsCardUpdateVisualsScopePatch
{
    private static void Postfix(NCard __instance)
    {
        BdDynamicOddsCardUi.EnforceCardUiScope(__instance);
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard._ExitTree))]
internal static class BdDynamicOddsCardExitTreeScopePatch
{
    private static void Prefix(NCard __instance)
    {
        BdDynamicOddsCardUi.EnforceCardUiScope(__instance);
    }
}
