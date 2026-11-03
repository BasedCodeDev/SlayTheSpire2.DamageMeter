using Godot;
using MegaCrit.Sts2.Core.Runs;
using static Based.Spires2DamageMeter.MeterPanel;
using static Godot.Control;
namespace Based.Spires2DamageMeter.Runtime;

internal static class MeterPanelLogic
{
    public static void Ensure()
    {
        if (GodotObject.IsInstanceValid(instance)) return;
        instance = new(); ((SceneTree)Engine.GetMainLoop()).Root.CallDeferred(Node.MethodName.AddChild, instance);
    }
    public static void _Ready(MeterPanel self)
    {
        self.Layer = 100; self.settings.Load(self.SettingsPath);
        self.panel = new PanelContainer { Position = self.settings.GetValue("layout", "position", new Vector2(20, 160)).AsVector2(), CustomMinimumSize = new Vector2(280, 0) }; self.AddChild(self.panel);
        self.panel.Theme = new Theme
        {
            DefaultFont = new SystemFont
            {
                FontNames = new[] { "ITC Avant Garde Gothic", "Avant Garde", "AvantGarde", "DejaVu Sans", "Segoe UI Light" }
            },
            DefaultFontSize = 16
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(.025f, .035f, .05f, .42f),
            BorderColor = new Color(.65f, .73f, .84f, .18f),
            ShadowColor = new Color(0, 0, 0, .2f)
        };
        panelStyle.SetContentMarginAll(5);
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetBorderWidthAll(1);
        panelStyle.ShadowSize = 8;
        self.panel.AddThemeStyleboxOverride("panel", panelStyle);
        self.expandedWidth = Math.Max(280, self.settings.GetValue("layout", "width", 380f).AsSingle());
        self.panel.Size = new Vector2(self.expandedWidth, 0);
        self.layout = new VBoxContainer(); self.layout.AddThemeConstantOverride("separation", 3); self.panel.AddChild(self.layout);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 2); self.layout.AddChild(header);
        self.collapse = new Button { Text = "-", TooltipText = "Collapse / expand", CustomMinimumSize = new Vector2(24, 26), FocusMode = Control.FocusModeEnum.None }; header.AddChild(self.collapse);
        self.metric = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FitToLongestItem = false, ClipText = true, CustomMinimumSize = new Vector2(0, 26) }; header.AddChild(self.metric);
        self.resize = new MeterHoverLabel { Text = "\u2194", TooltipText = "Meter controls", HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(20, 26) }; header.AddChild(self.resize);
        self.resize.GuiInput += e =>
        {
            if (e is InputEventMouseButton b && b.ButtonIndex == MouseButton.Left) { self.resizing = b.Pressed; self.dragging = false; }
            if (e is InputEventMouseMotion m) { if (self.resizing) self.panel.Size = new Vector2(Math.Max(280, self.panel.Size.X + m.Relative.X), self.panel.Size.Y); }
        };
        self.grab = new Button { Text = "\u2630", TooltipText = "Left-drag to move", FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.Drag, CustomMinimumSize = new Vector2(24, 26) }; header.AddChild(self.grab);
        self.grab.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } b) { self.dragging = b.Pressed; self.resizing = false; if (b.Pressed) self.CloseTooltips(); }
            if (self.dragging && e is InputEventMouseMotion m) self.panel.Position += m.Relative;
        };
        self.scopeIndex = Math.Clamp(self.settings.GetValue("layout", "scope", 0).AsInt32(), 0, 1);
        self.scopeToggle = new Button { CustomMinimumSize = new Vector2(26, 26), FocusMode = Control.FocusModeEnum.None, TooltipText = "Switch damage scope" }; header.AddChild(self.scopeToggle);
        self.scopeToggle.Pressed += () => { self.scopeIndex = 1 - self.scopeIndex; self.UpdateScopeToggle(); self.Save(); };
        self.UpdateScopeToggle();
        header.MoveChild(self.scopeToggle, 2);
        self.showUnattributed = self.settings.GetValue("layout", "showUnattributed", false).AsBool();
        self.unattributedToggle = new Button { Text = "?", ToggleMode = true, ButtonPressed = self.showUnattributed, CustomMinimumSize = new Vector2(22, 26), FocusMode = Control.FocusModeEnum.None };
        self.unattributedToggle.Toggled += value => { self.showUnattributed = value; self.UpdateUnattributedToggle(); self.Save(); };
        header.AddChild(self.unattributedToggle);
        header.MoveChild(self.unattributedToggle, 3);
        self.UpdateUnattributedToggle();
        self.body = new VBoxContainer(); self.body.AddThemeConstantOverride("separation", 2); self.layout.AddChild(self.body); self.collapse.Pressed += () => { self.ApplyCollapsedState(!self.collapsed); self.Save(); };
        self.metric.AddSeparator("Damage");
        for (var id = 0; id < 9; id++) { if (id == 8) self.metric.AddSeparator("Effective"); if (id == 5) self.metric.AddSeparator("Attribution"); self.metric.AddItem(MeterTooltipModel.Labels[id], id); self.metric.GetPopup().SetItemTooltip(self.metric.ItemCount - 1, MeterTooltipModel.Description(id)); if (id is 5 or 6) self.metric.SetItemDisabled(self.metric.ItemCount - 1, !NativeDamageCapture.Enabled); }
        var savedMetric = Math.Clamp(self.settings.GetValue("layout", "metric", 8).AsInt32(), 0, 8);
        if (savedMetric == 7 && self.settings.GetValue("layout", "attributionVersion", 0).AsInt32() < 1) savedMetric = 0;
        self.metric.Select(self.metric.GetItemIndex(savedMetric)); self.metric.ItemSelected += _ => self.Save();
        self.empty = new Label { Text = "No damage recorded yet" }; self.body.AddChild(self.empty);
        self.scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; self.body.AddChild(self.scroll);
        self.rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; self.rows.AddThemeConstantOverride("separation", 1); self.scroll.AddChild(self.rows);
        self.ApplyCollapsedState(self.settings.GetValue("layout", "collapsed", false).AsBool());
    }
    public static void UpdateScopeToggle(MeterPanel self)
    {
        self.scopeToggle.Text = self.scopeIndex == 0 ? "\u2694" : "\u21bb";
        self.scopeToggle.TooltipText = self.scopeIndex == 0 ? "Current Fight - click to show This Run" : "This Run - click to show Current Fight";
    }
    public static void UpdateUnattributedToggle(MeterPanel self)
    {
        self.unattributedToggle.TooltipText = self.showUnattributed ? "Unattributed damage shown - click to hide" : "Unattributed damage hidden - click to show";
    }
    public static Color Saturate(Color color, float amount)
    {
        var gray = .2126f * color.R + .7152f * color.G + .0722f * color.B;
        return new(
            Math.Clamp(gray + (color.R - gray) * amount, 0, 1),
            Math.Clamp(gray + (color.G - gray) * amount, 0, 1),
            Math.Clamp(gray + (color.B - gray) * amount, 0, 1),
            color.A);
    }
    public static Vector2 TooltipBounds(MeterPanel self, Vector2 viewport)
    {
        var width = Math.Min(Math.Max(240, self.panel.Size.X * 1.15f), Math.Max(160, viewport.X * .68f));
        var height = Math.Min(Math.Max(240, Math.Max(self.panel.Size.X, self.panel.Size.Y) * 1.15f), Math.Max(120, viewport.Y * .7f));
        return new(width, height);
    }
    public static void ApplyCollapsedState(MeterPanel self, bool value)
    {
        self.CloseTooltips();
        if (value && !self.collapsed) self.expandedWidth = self.panel.Size.X;
        self.collapsed = value; self.body.Visible = !value; self.metric.Visible = !value; self.scopeToggle.Visible = !value; self.unattributedToggle.Visible = !value; self.resize.Visible = !value; self.grab.Visible = !value;
        self.collapse.Text = value ? "+" : "-";
        self.panel.CustomMinimumSize = new Vector2(value ? 0 : 280, 0);
        self.panel.Size = new Vector2(value ? 0 : self.expandedWidth, 0);
        self.dragging = self.resizing = false;
        self.QueueHeightRefresh();
    }
    public static void QueueHeightRefresh(MeterPanel self)
    {
        if (self.heightRefreshPending) return;
        self.heightRefreshPending = true;
        // Hidden children leave the minimum-size calculation after the container's queued layout pass.
        self.layout.QueueSort();
        Hosting.RuntimeHost.Defer(() =>
        {
            self.heightRefreshPending = false;
            if (!GodotObject.IsInstanceValid(self.panel)) return;
            self.panel.Size = self.collapsed ? Vector2.Zero : new Vector2(self.panel.Size.X, 0);
        });
    }
    public static void Save(MeterPanel self)
    {
        self.settings.SetValue("layout", "position", self.panel.Position); self.settings.SetValue("layout", "width", self.collapsed ? self.expandedWidth : self.panel.Size.X); self.settings.SetValue("layout", "collapsed", self.collapsed);
        self.settings.SetValue("layout", "attributionVersion", 1); self.settings.SetValue("layout", "metric", self.Metric); self.settings.SetValue("layout", "scope", self.scopeIndex); self.settings.SetValue("layout", "showUnattributed", self.showUnattributed); self.settings.Save(self.SettingsPath);
    }
    public static void _Input(MeterPanel self, InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 }) { self.CloseTooltips(); self.dragging = self.resizing = false; self.panel.Visible = !self.panel.Visible; }
        if (e is InputEventMouseButton { Pressed: false }) { if (self.dragging || self.resizing) self.Save(); self.dragging = self.resizing = false; }
    }
    public static void _Process(MeterPanel self, double delta)
    {
        var viewport = self.GetViewport().GetVisibleRect().Size;
        var width = Math.Clamp(self.panel.Size.X, self.collapsed ? 0 : Math.Min(280, viewport.X), viewport.X);
        if (!Mathf.IsEqualApprox(width, self.panel.Size.X)) self.panel.Size = new Vector2(width, 0);
        self.panel.Position = new Vector2(Math.Clamp(self.panel.Position.X, 0, Math.Max(0, viewport.X - self.panel.Size.X)), Math.Clamp(self.panel.Position.Y, 0, Math.Max(0, viewport.Y - 40)));
        var tooltipBounds = self.TooltipBounds(viewport);
        self.elapsed += delta; if (self.elapsed < .1) return; self.elapsed = 0;
        var sessionType = RunManager.Instance?.NetService?.Type.ToString();
        var detectedSolo = MeterTooltipModel.SoloForSession(sessionType);
        if (detectedSolo.HasValue) self.solo = detectedSolo.Value;
        var actual = self.scopeIndex == 0 ? Probe.Meter.Rows : RunSession.RunRows();
        var effective = self.scopeIndex == 0 ? Probe.Effective : RunSession.EffectiveRun();
        var ledger = self.Metric == 8 ? effective.Ledger() : self.scopeIndex == 0 ? Probe.Credited : RunSession.CreditedRun();
        var transfers = self.scopeIndex == 0 ? Probe.Meter.Transfers : RunSession.CreditedTransfers();
        var credited = ledger.Rows(self.Metric == 8 ? null : transfers); foreach (var id in actual.Keys) credited.TryAdd(id, new());
        var data = self.Metric == 7 ? actual : credited;
        decimal Value(Totals t) => self.Metric switch { 1 => t.Hp, 2 => t.Block, 3 => t.Poison, 4 => t.Doom, 5 => t.AmplifiedHp + t.AmplifiedBlock, 6 => t.SupportGiven, 7 => t.Hp + t.Block, _ => t.Hp + t.Block };
        var visibleData = data.Where(p => self.showUnattributed || p.Key != StatusOwnershipLedger.Unknown).ToArray();
        var ordered = visibleData.OrderByDescending(p => actual.GetValueOrDefault(p.Key)?.Hp ?? 0).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray(); var max = ordered.Select(p => Value(p.Value)).DefaultIfEmpty().Max(); var team = visibleData.Sum(p => Value(p.Value));
        var hovered = self.panel.GetGlobalRect().HasPoint(self.GetViewport().GetMousePosition());
        var visibleIds = visibleData.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var id in self.controls.Keys.Except(visibleIds).ToArray()) { self.controls[id].Button.CloseTooltip(); self.controls[id].Button.QueueFree(); self.controls.Remove(id); }
        var breakdowns = self.Metric == 7 ? (self.scopeIndex == 0 ? Probe.Meter.StructuredSources : RunSession.SourceBreakdowns()) : ledger.Sources();
        var diagnostics = $"{Probe.Combat} | {Probe.Health}\n{RunSession.Status}\n{(self.scopeIndex == 1 && RunSession.AttributionPartial ? "Partial credited history; earlier actuals available in Actual Damage" : "")}\nAttribution: {ledger.Provisional} provisional/unknown events\nEnemy amplification: {NativeDamageCapture.Matched} matched, {NativeDamageCapture.Unmatched} unmeasured (audit pending)\n{Probe.OwnershipStatus}\nEffective: {(EffectiveCapture.Enabled ? "enabled" : "unavailable")}, {(effective.Incomplete ? "partial coverage" : "observed coverage")}";
        var badge = MeterTooltipModel.Status((self.Metric == 8 && (!EffectiveCapture.Enabled || effective.Incomplete)) || Probe.Health.StartsWith("INCOMPLETE") || !NativeDamageCapture.Enabled || !Probe.OwnershipAvailable || RunSession.StorageIncomplete, ledger.Provisional > 0 || NativeDamageCapture.Unmatched > 0 || (self.scopeIndex == 1 && (RunSession.AttributionPartial || RunSession.HistoryPartial)));
        self.resize.UpdateTooltip(new MeterTooltipModel($"Meter {Probe.Version}\nDrag arrow: resize; drag grab handle: move. Click the scope icon to switch between Current Fight and This Run.\nF8: show/hide. Bars scale to the largest player total.\nRows show rounded damage and, in multiplayer, share of the selected party metric.\n{(badge.Length > 0 ? badge + ": some statistics may be missing or unavailable." : "Capture status")}\nSelected: " + MeterTooltipModel.Description(self.Metric), Array.Empty<string>(), diagnostics) { ShowSources = false }, tooltipBounds);
        var metricTip = MeterTooltipModel.Description(self.Metric) + (badge.Length > 0 ? $"\n{badge}: some statistics may be missing or unavailable. Hover the resize arrow for details." : "");
        if (self.metric.TooltipText != metricTip) self.metric.TooltipText = metricTip;
        self.empty.Text = !self.showUnattributed && data.ContainsKey(StatusOwnershipLedger.Unknown) ? "No attributed damage yet" : "No damage recorded yet";
        self.empty.Visible = ordered.Length == 0;
        var index = 0;
        foreach (var (id, total) in ordered)
        {
            if (!self.controls.TryGetValue(id, out var row))
            {
                var button = new MeterHoverButton { TooltipText = "Player details", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 26) };
                button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(1, 1, 1, .05f) });
                self.rows.AddChild(button);
                var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; button.AddChild(line); line.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var icon = new TextureRect { CustomMinimumSize = new Vector2(18, 18), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore }; line.AddChild(icon);
                var bar = new MeterBar { CustomMinimumSize = new Vector2(70, 20), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore }; line.AddChild(bar);
                self.controls[id] = row = (button, icon, bar);
            }
            if (!hovered) self.rows.MoveChild(row.Button, index); index++;
            row.Icon.Texture = Probe.CharacterIcons.GetValueOrDefault(id);
            row.Icon.Visible = !self.solo && row.Icon.Texture != null;
            (Color Hp, Color Amp) colors = Probe.CharacterColors.GetValueOrDefault(id, (Colors.Gray, Colors.Gray));
            row.Bar.HpColour = Saturate(colors.Hp, 1.2f); row.Bar.AmpColour = Saturate(colors.Amp, 1.2f);
            var value = Value(total);
            row.Bar.PlayerName = Probe.Names.GetValueOrDefault(id, id);
            row.Bar.ShowPlayerName = !self.solo || id == StatusOwnershipLedger.Unknown;
            row.Bar.DamageText = Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.CurrentCulture);
            row.Bar.ShareText = team > 0 ? $"{100 * value / team:0.#}%" : "0%";
            row.Bar.ShowDetails = !self.solo;
            row.Bar.Orange = self.Metric switch { 0 or 8 => total.AmplifiedHp + total.AmplifiedBlock, 1 => total.AmplifiedHp, 2 => total.AmplifiedBlock, 5 => value, 6 => value, 7 => total.AmplifiedHp + total.AmplifiedBlock, 3 => ledger.Credits.Where(c => c.Player == id && c.Kind == "Poison" && c.Amplification).Sum(c => c.Hp), _ => 0 };
            row.Bar.Block = self.Metric switch { 0 or 8 => Math.Max(0, total.Block - total.AmplifiedBlock), 2 => Math.Max(0, total.Block - total.AmplifiedBlock), 7 => Math.Max(0, total.Block - total.AmplifiedBlock), _ => 0 };
            row.Bar.Hp = Math.Max(0, value - row.Bar.Block - row.Bar.Orange); row.Bar.Maximum = max; row.Bar.QueueRedraw();
            var affected = (self.Metric == 8 && (!EffectiveCapture.Enabled || effective.Incomplete)) || Probe.Health.StartsWith("INCOMPLETE") || !Probe.OwnershipAvailable || (self.Metric != 7 && (!NativeDamageCapture.Enabled || ledger.Provisional > 0 || NativeDamageCapture.Unmatched > 0)) || (self.scopeIndex == 1 && (RunSession.StorageIncomplete || RunSession.HistoryPartial || (self.Metric != 7 && RunSession.AttributionPartial)));
            var card = MeterTooltipModel.BuildCard(id, row.Bar.PlayerName, self.Metric, self.scopeIndex == 1, self.solo, value, team, total, effective.PendingFor(id), row.Bar.Orange, breakdowns, transfers, p => Probe.Names.GetValueOrDefault(p, p), affected ? "Data may be incomplete. Hover the resize arrow for details." : "");
            row.Button.UpdateTooltip(new("", Array.Empty<string>(), "") { Card = card }, tooltipBounds);

        }
        var overhead = self.panel.GetCombinedMinimumSize().Y - self.scroll.CustomMinimumSize.Y;
        var desired = Math.Min(Math.Max(0, viewport.Y - self.panel.Position.Y - overhead - 8), Math.Max(0, self.controls.Count * 28));
        self.scroll.CustomMinimumSize = new Vector2(0, desired);
        self.QueueHeightRefresh();
    }
    public static void CloseTooltips(MeterPanel self) { if (self.resize != null) self.resize.CloseTooltip(); foreach (var row in self.controls.Values) row.Button.CloseTooltip(); }
    public static void _ExitTree(MeterPanel self) { self.CloseTooltips(); self.Save(); Probe.Flush(); if (ReferenceEquals(instance, self)) instance = null; }
}