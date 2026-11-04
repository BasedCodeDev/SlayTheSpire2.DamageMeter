using Godot;
using System.Globalization;
namespace Based.Spires2DamageMeter.Runtime;

internal static class BarAnimations
{
    private static readonly Dictionary<MeterBar, BarMotion> states = new();
    public static BarFrame Exact(MeterBar bar, decimal value, decimal share)
    {
        var baseFraction = bar.Maximum > 0 ? Math.Clamp((bar.Hp + bar.Block) / bar.Maximum, 0, 1) : 0;
        var endFraction = bar.Maximum > 0 ? Math.Clamp((bar.Hp + bar.Block + bar.Orange) / bar.Maximum, baseFraction, 1) : 0;
        return new(baseFraction, endFraction, Math.Max(0,value), Math.Clamp(share,0,100));
    }
    public static void Set(MeterPanel meter, MeterBar bar, decimal value, decimal share)
    {
        if (!states.TryGetValue(bar, out var state)) states[bar] = state = new();
        var context = $"{meter.Metric}/{meter.scopeIndex}/{meter.showUnattributed}/{Probe.Combat}/{RunSession.generation}/{RunSession.Status}";
        state.Set(Exact(bar,value,share), context, !meter.collapsed && meter.panel.Visible);
    }
    public static BarFrame Frame(MeterBar bar) => states.TryGetValue(bar, out var state) ? state.Current : Exact(bar,0,0);
    public static string Text(MeterBar bar, bool target = false)
    {
        if (!states.TryGetValue(bar, out var state)) return bar.ShowDetails ? $"{bar.DamageText} | {bar.ShareText}" : bar.DamageText;
        var frame = target ? state.Target : state.Current;
        return Format(frame,bar.ShowDetails);
    }
    private static string Format(BarFrame frame, bool details)
    {
        var damage = Math.Round(frame.Value, MidpointRounding.AwayFromZero).ToString("0",CultureInfo.CurrentCulture);
        return details ? $"{damage} | {frame.Share.ToString("0.#",CultureInfo.CurrentCulture)}%" : damage;
    }
    public static string StartText(MeterBar bar) => states.TryGetValue(bar,out var state) ? Format(state.Start,bar.ShowDetails) : Text(bar);
    public static float Pulse(MeterBar bar) => states.TryGetValue(bar, out var state) ? state.Pulse : 0;
    public static void Advance(MeterPanel meter, double delta)
    {
        foreach (var row in meter.controls.Values)
        {
            if (!states.TryGetValue(row.Bar,out var state)) continue;
            var redraw = state.Active;
            if (meter.collapsed || !meter.panel.Visible) state.Snap(state.Target,state.Context!);
            else state.Advance(delta);
            if (redraw) row.Bar.QueueRedraw();
        }
    }
    public static void Forget(MeterBar bar) => states.Remove(bar);
    public static void Dispose() => states.Clear();
}
