using Godot;
namespace Based.Spires2DamageMeter.Runtime;

internal static class FloatingHeader
{
    public static void Create(MeterPanel meter, HBoxContainer header)
    {
        var strip = new PanelContainer { Name = "HoverHeader", Theme = meter.panel.Theme, Visible = false };
        var style = (StyleBoxFlat)meter.panel.GetThemeStylebox("panel").Duplicate();
        style.ContentMarginTop = style.ContentMarginBottom = 0;
        strip.AddThemeStyleboxOverride("panel", style);
        meter.AddChild(strip);
        header.GetParent().RemoveChild(header); strip.AddChild(header);
        strip.SetMeta("outside", .2d);
    }

    public static void SetCollapsed(MeterPanel meter, bool collapsed)
    {
        var strip = meter.GetNode<PanelContainer>("HoverHeader");
        var header = meter.collapse.GetParent();
        var parent = collapsed ? (Node)meter.layout : strip;
        if (header.GetParent() != parent) { header.GetParent().RemoveChild(header); parent.AddChild(header); }
        if (collapsed) { meter.layout.MoveChild(header, 0); strip.Hide(); }
    }

    public static void Update(MeterPanel meter, double delta)
        => Update(meter, delta, meter.GetViewport().GetMousePosition());

    internal static void Update(MeterPanel meter, double delta, Vector2 pointer)
    {
        var strip = meter.GetNode<PanelContainer>("HoverHeader");
        if (meter.collapsed || !meter.panel.Visible) { strip.Hide(); strip.SetMeta("outside", .2d); return; }
        var viewport = meter.GetViewport().GetVisibleRect().Size;
        var height = strip.GetCombinedMinimumSize().Y;
        strip.Size = new Vector2(meter.panel.Size.X, height);
        var y = meter.panel.Position.Y >= height ? meter.panel.Position.Y - height : meter.panel.Position.Y + meter.panel.Size.Y;
        strip.Position = new Vector2(Math.Clamp(meter.panel.Position.X, 0, Math.Max(0, viewport.X - strip.Size.X)), Math.Clamp(y, 0, Math.Max(0, viewport.Y - height)));
        var hover = meter.panel.GetGlobalRect().HasPoint(pointer) || (strip.Visible && strip.GetGlobalRect().HasPoint(pointer));
        var keep = hover || meter.dragging || meter.resizing || meter.metric.GetPopup().Visible;
        var outside = keep ? 0 : strip.GetMeta("outside").AsDouble() + Math.Max(0, delta);
        strip.SetMeta("outside", outside);
        strip.Visible = outside < .2;
    }

    public static void MeasureRows(MeterPanel meter)
    {
        if (meter.collapsed) return;
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root.GetVisibleRect().Size;
        var overhead = meter.panel.GetThemeStylebox("panel").GetMinimumSize().Y;
        if (meter.empty.Visible) overhead += meter.empty.GetCombinedMinimumSize().Y + meter.body.GetThemeConstant("separation");
        var padding = meter.scroll.HasThemeStylebox("panel") ? meter.scroll.GetThemeStylebox("panel").GetMinimumSize().Y : 0;
        var content = Math.Ceiling(meter.rows.GetCombinedMinimumSize().Y + padding);
        var available = Math.Max(0, Math.Floor(viewport.Y - meter.panel.Position.Y - overhead - 8));
        meter.scroll.CustomMinimumSize = new Vector2(0, (float)Math.Min(content, available));
    }
}
