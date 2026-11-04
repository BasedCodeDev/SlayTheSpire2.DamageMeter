using Godot;

namespace Based.Spires2DamageMeter.Runtime;

internal static class EdgePlacement
{
    private static Vector2 Viewport => ((SceneTree)Engine.GetMainLoop()).Root.GetVisibleRect().Size;

    public static bool Update(MeterPanel meter)
    {
        var panel = meter.panel;
        var right = panel.GetMeta("collapse_right", false).AsBool();
        if (!panel.HasMeta("collapse_anchor"))
        {
            var distance = panel.Position.X + panel.Size.X / 2 - Viewport.X / 2;
            if (!Mathf.IsZeroApprox(distance)) right = distance > 0;
        }
        panel.SetMeta("collapse_right", right);
        var header = meter.collapse.GetParent();
        var index = right ? header.GetChildCount() - 1 : 0;
        if (meter.collapse.GetIndex() != index) header.MoveChild(meter.collapse, index);
        return right;
    }

    public static void Begin(MeterPanel meter, bool value, bool preserveEdge)
    {
        if (!preserveEdge)
        {
            if (meter.panel.HasMeta("collapse_anchor")) meter.panel.RemoveMeta("collapse_anchor");
            return;
        }
        if (value != meter.collapsed && Update(meter))
            meter.panel.SetMeta("collapse_anchor", meter.panel.GetMeta("collapse_anchor", meter.panel.Position.X + meter.panel.Size.X));
    }

    public static void Settle(MeterPanel meter)
    {
        var anchored = meter.panel.HasMeta("collapse_anchor");
        if (anchored)
        {
            var edge = meter.panel.GetMeta("collapse_anchor").AsSingle();
            meter.panel.Position = new Vector2(edge - meter.panel.Size.X, meter.panel.Position.Y);
            meter.panel.RemoveMeta("collapse_anchor");
        }
        Clamp(meter);
        Update(meter);
        if (anchored) meter.Save();
    }

    public static void Clamp(MeterPanel meter)
    {
        var viewport = Viewport;
        meter.panel.Position = new Vector2(Math.Clamp(meter.panel.Position.X, 0, Math.Max(0, viewport.X - meter.panel.Size.X)),
            Math.Clamp(meter.panel.Position.Y, 0, Math.Max(0, viewport.Y - 40)));
    }
}
