using Godot;
namespace Based.Spires2DamageMeter.Runtime;

internal static class RowAnimations
{
    private sealed class Move { public float Current, Start, Target; public double Elapsed = .2; public bool Ready; }
    private sealed class State { public string Context = ""; public bool Pending; public readonly Dictionary<MeterHoverButton,Move> Moves = new(); }
    private static readonly Dictionary<MeterPanel,State> states = new();
    public static bool IsMoving(MeterPanel meter) => states.TryGetValue(meter,out var state) && (state.Pending || state.Moves.Values.Any(m => m.Elapsed < .2));
    public static void Reorder(MeterPanel meter, IReadOnlyList<MeterHoverButton> ordered)
    {
        if (!states.TryGetValue(meter,out var state)) states[meter] = state = new();
        var context = $"{Probe.Combat}/{RunSession.generation}/{RunSession.Status}";
        var snap = context != state.Context || meter.collapsed || !meter.panel.Visible;
        state.Context = context;
        var changed = snap;
        foreach (var stale in state.Moves.Keys.Except(ordered).ToArray()) state.Moves.Remove(stale);
        for (int i = 0; i < ordered.Count; i++)
        {
            var button = ordered[i];
            if (!state.Moves.ContainsKey(button)) { state.Moves[button] = new(); changed = true; }
            var slot = button.GetParent();
            if (slot.GetIndex() != i) { meter.rows.MoveChild(slot,i); changed = true; }
        }
        if (!changed) return;
        meter.rows.QueueSort();
        state.Pending = true;
        Hosting.RuntimeHost.Defer(() =>
        {
            if (!GodotObject.IsInstanceValid(meter) || !meter.IsInsideTree()) return;
            state.Pending = false;
            foreach (var (button, move) in state.Moves)
            {
                if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree()) continue;
                var target = ((Control)button.GetParent()).Position.Y;
                if (snap || !move.Ready || meter.collapsed || !meter.panel.Visible)
                { move.Current = move.Start = move.Target = target; move.Elapsed = .2; move.Ready = true; }
                else if (move.Target != target) { move.Start = move.Current; move.Target = target; move.Elapsed = 0; }
                Place(button,move);
            }
        });
    }
    private static void Place(MeterHoverButton button, Move move)
    {
        var slot = (Control)button.GetParent();
        button.Position = new Vector2(0,move.Current - slot.Position.Y);
        slot.ZIndex = move.Elapsed < .2 && move.Target < move.Start ? 1 : 0;
        button.MouseFilter = move.Elapsed < .2 ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
    }
    public static void Advance(MeterPanel meter, double delta)
    {
        if (!states.TryGetValue(meter,out var state)) return;
        foreach (var (button,move) in state.Moves)
        {
            if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree() || !move.Ready) continue;
            if (meter.collapsed || !meter.panel.Visible) { move.Current = move.Target; move.Elapsed = .2; }
            else
            {
                move.Elapsed = Math.Min(.2,move.Elapsed + Math.Max(0,delta));
                var t = 1 - Math.Pow(1 - move.Elapsed/.2,3);
                move.Current = move.Elapsed >= .2 ? move.Target : move.Start + (move.Target - move.Start)*(float)t;
            }
            Place(button,move);
        }
    }
    public static void Dispose() => states.Clear();
}
