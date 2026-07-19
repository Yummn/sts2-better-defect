using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace BetterDefect;

internal static partial class BdDynamicOddsStatsHud
{
    private const string LayerName = "BetterDefectDisableStatsLayer";
    private const string HudName = "BetterDefectDisableStatsHud";
    private const int MaxPoints = BdDynamicOdds.MaxCardPointBudget;
    private const int BlueLimit = 25;

    private static CanvasLayer? _layer;
    private static DisableStatsHud? _hud;
    private static ulong _lastStatsContextMsec;
    private static bool _loggedInstalled;
    private static bool _loggedAttachDeferred;
    private static bool _loggedVisible;

    public static void EnsureInstalled()
    {
        try
        {
            if (_hud is not null && GodotObject.IsInstanceValid(_hud) && _hud.IsInsideTree())
                return;

            var tree = Engine.GetMainLoop() as SceneTree;
            var root = tree?.Root;
            if (root is null)
                return;

            if (_hud is not null && GodotObject.IsInstanceValid(_hud) &&
                _layer is not null && GodotObject.IsInstanceValid(_layer))
            {
                if (!_layer.IsInsideTree() && _layer.GetParent() is null)
                    root.CallDeferred("add_child", _layer);
                if (_hud.GetParent() is null)
                    _layer.CallDeferred("add_child", _hud);
                return;
            }

            var existingLayer = root.GetNodeOrNull<CanvasLayer>(LayerName);
            if (existingLayer is not null)
            {
                _layer = existingLayer;
                _hud = existingLayer.GetNodeOrNull<DisableStatsHud>(HudName);
                if (_hud is not null && GodotObject.IsInstanceValid(_hud) && _hud.IsInsideTree())
                    return;
            }

            _layer = existingLayer ?? new CanvasLayer
            {
                Name = LayerName,
                Layer = 120,
                ProcessMode = Node.ProcessModeEnum.Always
            };
            if (existingLayer is null)
                root.CallDeferred("add_child", _layer);

            _hud = new DisableStatsHud
            {
                Name = HudName,
                Visible = false
            };
            _hud.Build();
            if (_layer.IsInsideTree())
                _layer.AddChild(_hud);
            else
                _layer.CallDeferred("add_child", _hud);
            _hud.Refresh(BdDynamicOdds.GetUsedCardPointCount(), BdDynamicOdds.GetDisabledCardCount(), BdDynamicOdds.GetVersionUpgradeCount());

            if (!_loggedInstalled)
            {
                _loggedInstalled = true;
                MainFile.Logger.Info("[BetterDefect] disable stats HUD scheduled on top canvas layer.");
            }
            if (!_loggedAttachDeferred)
            {
                _loggedAttachDeferred = true;
                MainFile.Logger.Info("[BetterDefect] disable stats HUD uses deferred attach to avoid Android startup add_child race.");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[BetterDefect] disable stats HUD install failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void RefreshFrom(NCard cardNode)
    {
        EnsureInstalled();
        if (_hud is null || !GodotObject.IsInstanceValid(_hud))
            return;

        if (IsStatsContext(cardNode))
        {
            _lastStatsContextMsec = Time.GetTicksMsec();
            ShowAndRefresh();
        }
    }

    public static void ShowForLibrary()
    {
        EnsureInstalled();
        if (_hud is null || !GodotObject.IsInstanceValid(_hud))
            return;

        try { _lastStatsContextMsec = Time.GetTicksMsec(); } catch { }
        ShowAndRefresh();
    }

    public static void Hide()
    {
        try
        {
            _lastStatsContextMsec = 0;
            if (_hud is not null && GodotObject.IsInstanceValid(_hud))
            {
                _hud.Visible = false;
                _hud.SetProcess(false);
            }
        }
        catch { }
    }

    public static void HideIfOutsideLibrary(Node node)
    {
        try
        {
            if (_hud is null || !GodotObject.IsInstanceValid(_hud) || !_hud.Visible)
                return;

            if (!IsStatsContext(node))
                Hide();
        }
        catch { }
    }

    private static void ShowAndRefresh()
    {
        if (_hud is null || !GodotObject.IsInstanceValid(_hud))
            return;

        _hud.Visible = true;
        _hud.SetProcess(true);
        var disabledCount = BdDynamicOdds.GetDisabledCardCount();
        var upgradeCount = BdDynamicOdds.GetVersionUpgradeCount();
        var usedPoints = disabledCount + upgradeCount;
        _hud.Refresh(usedPoints, disabledCount, upgradeCount);

        if (!_loggedVisible)
        {
            _loggedVisible = true;
            MainFile.Logger.Info($"[BetterDefect] card-point HUD visible; used={usedPoints}/{MaxPoints}, disabled={disabledCount}, upgraded={upgradeCount}.");
        }
    }

    private static bool HasRecentStatsContext()
    {
        if (_lastStatsContextMsec == 0)
            return false;

        try
        {
            return Time.GetTicksMsec() - _lastStatsContextMsec <= 800UL;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldForceHideFromTree()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var root = tree?.Root;
            if (root is null)
                return false;

            return HasVisibleNodeName(root, 0, 14, "NCombat", "CombatRoom", "RewardScreen", "Merchant", "ShopScreen");
        }
        catch { return false; }
    }

    private static bool ShouldShowFromTree()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var root = tree?.Root;
            if (root is null)
                return false;

            return HasVisibleCardLibrary(root, 0, 48);
        }
        catch { return false; }
    }

    private static bool IsStatsContext(Node node)
    {
        if (HasAncestorName(node, "NCombat", "CombatRoom", "Reward", "Merchant", "Shop"))
            return false;
        if (HasCardLibraryAncestor(node))
            return true;
        return false;
    }

    private static bool HasCardLibraryAncestor(Node node)
    {
        for (var n = node; n != null; n = n.GetParent())
        {
            if (n is NCardLibrary)
                return true;
        }
        return false;
    }

    private static bool HasAncestorName(Node node, params string[] needles)
    {
        for (var n = node; n != null; n = n.GetParent())
        {
            var typeName = n.GetType().FullName ?? n.GetType().Name;
            var nodeName = n.Name.ToString();
            if (ContainsAny(typeName, needles) || ContainsAny(nodeName, needles))
                return true;
        }
        return false;
    }

    private static bool HasVisibleCardLibrary(Node node, int depth, int maxDepth)
    {
        if (depth > maxDepth)
            return false;

        try
        {
            if (node is CanvasItem item && !item.IsVisibleInTree())
                return false;
        }
        catch { }

        if (node is NCardLibrary)
            return true;

        var childCount = node.GetChildCount();
        for (var i = 0; i < childCount; i++)
        {
            if (HasVisibleCardLibrary(node.GetChild(i), depth + 1, maxDepth))
                return true;
        }
        return false;
    }

    private static bool HasVisibleNodeName(Node node, int depth, int maxDepth, params string[] needles)
    {
        if (depth > maxDepth)
            return false;

        try
        {
            if (node is CanvasItem item && !item.IsVisibleInTree())
                return false;
        }
        catch { }

        var typeName = node.GetType().FullName ?? node.GetType().Name;
        var nodeName = node.Name.ToString();
        if (ContainsAny(typeName, needles) || ContainsAny(nodeName, needles))
            return true;

        var childCount = node.GetChildCount();
        for (var i = 0; i < childCount; i++)
        {
            if (HasVisibleNodeName(node.GetChild(i), depth + 1, maxDepth, needles))
                return true;
        }
        return false;
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
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

    private sealed partial class DisableStatsHud : PanelContainer
    {
        private readonly List<Panel> _segments = [];
        private readonly StyleBoxFlat[,] _segmentStyles = new StyleBoxFlat[2, 2];
        private Label? _title;
        private Label? _counter;
        private double _scanTimer;
        private int _lastClampedCount = -1;

        public void Build()
        {
            AnchorLeft = 0.5f;
            AnchorRight = 0.5f;
            AnchorTop = 0f;
            AnchorBottom = 0f;
            OffsetLeft = -360f;
            OffsetRight = 360f;
            OffsetTop = 8f;
            OffsetBottom = 68f;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 4096;
            SetProcess(false);

            AddThemeStyleboxOverride("panel", MakePanelStyle());
            _segmentStyles[0, 0] = MakeSegmentStyle(filled: false, danger: false);
            _segmentStyles[0, 1] = MakeSegmentStyle(filled: false, danger: true);
            _segmentStyles[1, 0] = MakeSegmentStyle(filled: true, danger: false);
            _segmentStyles[1, 1] = MakeSegmentStyle(filled: true, danger: true);

            var root = new VBoxContainer
            {
                Name = "Root",
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            root.AddThemeConstantOverride("separation", 4);
            AddChild(root);

            var header = new HBoxContainer
            {
                Name = "Header",
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            header.AddThemeConstantOverride("separation", 6);
            root.AddChild(header);

            header.AddChild(MakeOrnament("◆"));
            _title = MakeLabel("机器人改造点数", 14, HorizontalAlignment.Left);
            _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            header.AddChild(_title);

            _counter = MakeLabel("0/35", 13, HorizontalAlignment.Right);
            header.AddChild(_counter);
            header.AddChild(MakeOrnament("◆"));

            var barFrame = new PanelContainer
            {
                Name = "SegmentFrame",
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(0f, 20f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            barFrame.AddThemeStyleboxOverride("panel", MakeBarFrameStyle());
            root.AddChild(barFrame);

            var bar = new HBoxContainer
            {
                Name = "Segments",
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            bar.AddThemeConstantOverride("separation", 3);
            barFrame.AddChild(bar);

            for (var i = 0; i < MaxPoints; i++)
            {
                var segment = new Panel
                {
                    Name = $"Segment{i + 1}",
                    MouseFilter = MouseFilterEnum.Ignore,
                    CustomMinimumSize = new Vector2(9f, 13f),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                _segments.Add(segment);
                bar.AddChild(segment);
            }
        }

        public override void _Process(double delta)
        {
            // The HUD is normally driven by NCardLibrary open/close, but some
            // mobile flows do not deliver the close callback before combat is
            // restored.  Scan only while the HUD is already visible, and only a
            // few times per second, so combat/draw/play frames do not pay the
            // old always-on Android tree-walk cost.
            if (!Visible)
            {
                SetProcess(false);
                return;
            }

            _scanTimer += delta;
            if (_scanTimer < 0.35)
                return;

            _scanTimer = 0;

            if (ShouldForceHideFromTree())
            {
                Visible = false;
                SetProcess(false);
                return;
            }

            var inCardLibrary = HasRecentStatsContext() || ShouldShowFromTree();
            if (inCardLibrary)
                ShowAndRefresh();
            else
            {
                Visible = false;
                SetProcess(false);
            }
        }

        public void Refresh(int usedPointCount, int disabledCount, int upgradeCount)
        {
            var clamped = Math.Clamp(usedPointCount, 0, MaxPoints);
            if (_lastClampedCount == clamped)
                return;
            _lastClampedCount = clamped;

            var danger = clamped > BlueLimit;

            if (_title is not null)
            {
                _title.Text = danger
                    ? $"机器人改造点数  禁用{disabledCount}·升级{upgradeCount}  过载"
                    : $"机器人改造点数  禁用{disabledCount}·升级{upgradeCount}";
                _title.AddThemeColorOverride("font_color", danger
                    ? new Color(1f, 0.48f, 0.34f, 1f)
                    : new Color(0.70f, 0.86f, 1f, 1f));
            }

            if (_counter is not null)
            {
                _counter.Text = $"{clamped}/{MaxPoints}";
                _counter.AddThemeColorOverride("font_color", danger
                    ? new Color(1f, 0.55f, 0.36f, 1f)
                    : new Color(0.77f, 0.92f, 1f, 1f));
            }

            for (var i = 0; i < _segments.Count; i++)
            {
                var filled = i < clamped;
                var segmentDanger = i >= BlueLimit;
                _segments[i].AddThemeStyleboxOverride("panel", _segmentStyles[filled ? 1 : 0, segmentDanger ? 1 : 0]);
            }
        }

        private static StyleBoxFlat MakePanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.255f, 0.135f, 0.055f, 0.94f),
                BorderColor = new Color(0.075f, 0.035f, 0.012f, 0.98f),
                BorderWidthLeft = 5,
                BorderWidthTop = 5,
                BorderWidthRight = 5,
                BorderWidthBottom = 5,
                CornerRadiusTopLeft = 22,
                CornerRadiusTopRight = 14,
                CornerRadiusBottomLeft = 14,
                CornerRadiusBottomRight = 22,
                ContentMarginLeft = 12,
                ContentMarginTop = 4,
                ContentMarginRight = 12,
                ContentMarginBottom = 6,
                ShadowColor = new Color(0.02f, 0.012f, 0.006f, 0.38f),
                ShadowSize = 4,
                ShadowOffset = new Vector2(2f, 3f)
            };
        }

        private static StyleBoxFlat MakeBarFrameStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.105f, 0.055f, 0.025f, 0.78f),
                BorderColor = new Color(0.62f, 0.42f, 0.18f, 0.88f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 10,
                ContentMarginLeft = 5,
                ContentMarginTop = 3,
                ContentMarginRight = 5,
                ContentMarginBottom = 3
            };
        }

        private static StyleBoxFlat MakeSegmentStyle(bool filled, bool danger)
        {
            var bg = filled
                ? danger
                    ? new Color(0.82f, 0.18f, 0.10f, 0.96f)
                    : new Color(0.18f, 0.47f, 0.86f, 0.94f)
                : danger
                    ? new Color(0.24f, 0.075f, 0.05f, 0.58f)
                    : new Color(0.055f, 0.14f, 0.22f, 0.56f);

            var border = danger
                ? new Color(0.50f, 0.14f, 0.08f, 0.78f)
                : new Color(0.14f, 0.32f, 0.50f, 0.78f);

            return new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = border,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 4
            };
        }

        private static Label MakeLabel(string text, int fontSize, HorizontalAlignment alignment)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = alignment,
                MouseFilter = MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeConstantOverride("outline_size", 2);
            label.AddThemeColorOverride("font_color", new Color(0.98f, 0.87f, 0.62f, 1f));
            label.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.035f, 0.012f, 0.96f));
            return label;
        }

        private static Label MakeOrnament(string text)
        {
            var label = MakeLabel(text, 12, HorizontalAlignment.Center);
            label.AddThemeColorOverride("font_color", new Color(0.88f, 0.55f, 0.20f, 0.94f));
            return label;
        }
    }
}
