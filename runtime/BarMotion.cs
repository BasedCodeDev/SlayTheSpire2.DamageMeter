namespace Based.Spires2DamageMeter.Runtime;

internal readonly record struct BarFrame(decimal Base, decimal End, decimal Value, decimal Share);
internal sealed class BarMotion
{
    public BarFrame Current { get; private set; }
    public BarFrame Target { get; private set; }
    public BarFrame Start { get; private set; }
    public string? Context { get; private set; }
    private double elapsed = .2, pulseAge = .3, cooldown = .5;
    public bool Active => elapsed < .2 || pulseAge < .3;
    public float Pulse => (float)Math.Max(0, 1 - pulseAge / .3) * .12f;
    public void Snap(BarFrame target, string context)
    {
        Current = Start = Target = target; Context = context; elapsed = .2; pulseAge = .3; cooldown = .5;
    }
    public void Set(BarFrame target, string context, bool visible)
    {
        if (Context != context || !visible) { Snap(target, context); return; }
        if (target == Target) return;
        if (target.Value > Target.Value && cooldown >= .5) { pulseAge = 0; cooldown = 0; }
        Start = Current; Target = target; elapsed = 0;
    }
    public void Advance(double delta)
    {
        var dt = Math.Max(0, delta);
        elapsed = Math.Min(.2, elapsed + dt); pulseAge = Math.Min(.3, pulseAge + dt); cooldown = Math.Min(.5, cooldown + dt);
        var t = elapsed / .2;
        var eased = (decimal)(1 - Math.Pow(1 - t, 3));
        decimal Lerp(decimal a, decimal b) => a + (b - a) * eased;
        Current = elapsed >= .2 ? Target : new(Lerp(Start.Base, Target.Base), Lerp(Start.End, Target.End), Lerp(Start.Value, Target.Value), Lerp(Start.Share, Target.Share));
    }
}
