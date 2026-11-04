using Godot;
namespace Based.Spires2DamageMeter.Runtime;

internal static class PlayerTooltips
{
    private sealed class State
    {
        public PanelContainer? Panel;
        public MeterHoverButton? Source;
        public Vector2 Anchor;
        public double Outside;
        public float Alpha;
    }
    private static readonly Dictionary<MeterPanel, State> states = new();
    public static bool IsOpen(MeterPanel meter) => states.TryGetValue(meter, out var state) && GodotObject.IsInstanceValid(state.Panel);
    public static bool IsHovered(MeterPanel meter, Vector2 pointer) => states.TryGetValue(meter, out var state) && GodotObject.IsInstanceValid(state.Panel) && state.Panel!.Visible && state.Panel.GetGlobalRect().HasPoint(pointer);
    private static void Close(State state)
    {
        if (GodotObject.IsInstanceValid(state.Panel)) { state.Panel!.Hide(); state.Panel.QueueFree(); }
        state.Panel = null; state.Source = null; state.Alpha = 0; state.Outside = 0;
    }
    public static void CloseFor(MeterHoverButton source)
    {
        foreach (var state in states.Values) if (state.Source == source) Close(state);
    }
    public static void Dispose() { foreach (var state in states.Values) Close(state); states.Clear(); }
    public static void Update(MeterPanel meter, double delta) => Update(meter, delta, meter.GetViewport().GetMousePosition());
    internal static void Update(MeterPanel meter, double delta, Vector2 pointer)
    {
        if (!states.TryGetValue(meter, out var state)) states[meter] = state = new();
        if (!meter.panel.Visible || meter.collapsed || meter.dragging || meter.resizing)
        { Close(state); return; }
        if (state.Source != null && (!GodotObject.IsInstanceValid(state.Source) || !state.Source.IsInsideTree() || !meter.controls.Values.Any(r => r.Button == state.Source))) Close(state);
        var overPopup = IsHovered(meter, pointer);
        var source = overPopup ? state.Source : meter.controls.Values.Select(r => r.Button).FirstOrDefault(b => b.IsVisibleInTree() && b.GetGlobalRect().HasPoint(pointer));
        if (source != null && source != state.Source)
        {
            var alpha = state.Alpha;
            Close(state);
            state.Source = source; state.Alpha = alpha;
            var panel = new PanelContainer { Name = "PlayerTooltip", Theme = meter.panel.Theme, MouseFilter = Control.MouseFilterEnum.Stop, FocusMode = Control.FocusModeEnum.None };
            var style = (StyleBoxFlat)meter.panel.GetThemeStylebox("panel").Duplicate();
            style.BgColor = new Color(.035f,.04f,.055f,.98f); style.SetContentMarginAll(4);
            panel.AddThemeStyleboxOverride("panel", style);
            state.Panel = panel;
            var content = MeterHoverButtonLogic.CreateContent(source);
            content.SetMeta("interactive", true);
            meter.AddChild(panel); panel.AddChild(content);
            state.Anchor = source.GetGlobalRect().Position;
        }
        if (!GodotObject.IsInstanceValid(state.Panel)) return;
        var dt = Math.Max(0, delta);
        var previousOutside = state.Outside;
        var keep = source != null || overPopup;
        state.Outside = keep ? 0 : state.Outside + dt;
        var fadeDelta = keep ? dt : Math.Max(0, state.Outside - Math.Max(.2, previousOutside));
        state.Alpha = Mathf.MoveToward(state.Alpha, keep ? 1 : state.Outside >= .2 ? 0 : state.Alpha, (float)(fadeDelta / .15));
        if (!keep && state.Outside >= .2 && state.Alpha <= 0) { Close(state); return; }
        state.Panel!.Modulate = new Color(1,1,1,state.Alpha);
        var viewport = meter.GetViewport().GetVisibleRect().Size;
        var contentSize = new Vector2(Math.Max(32, Math.Min(state.Source!.availableSize.X, viewport.X - 24)), Math.Max(32, Math.Min(state.Source.availableSize.Y, viewport.Y - 24)));
        var popup = state.Source.popup!;
        if (popup.AvailableSize != contentSize) { popup.AvailableSize = contentSize; popup.Refresh(); }
        state.Panel.Size = state.Panel.GetCombinedMinimumSize();
        var x = meter.panel.Position.X + meter.panel.Size.X + 6;
        if (x + state.Panel.Size.X > viewport.X) x = meter.panel.Position.X - state.Panel.Size.X - 6;
        state.Panel.Position = new Vector2(Math.Clamp(x, 0, Math.Max(0, viewport.X - state.Panel.Size.X)), Math.Clamp(state.Anchor.Y, 0, Math.Max(0, viewport.Y - state.Panel.Size.Y)));
    }
}
