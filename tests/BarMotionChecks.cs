using Based.Spires2DamageMeter.Runtime;
static class BarMotionChecks
{
    public static void Run(Action<bool,string> check)
    {
        var state = new BarMotion(); var zero = new BarFrame(0,0,0,0);
        var first = new BarFrame(.6m,1,100,60);
        state.Set(zero,"fight",true); state.Set(first,"fight",true); state.Advance(.1);
        check(state.Current.Value == 87.5m && state.Current.Base == .525m && state.Current.Share == 52.5m,"bar motion 200ms cubic ease-out synchronizes counts and segments");
        check(state.Pulse > 0,"positive contribution starts restrained pulse");
        var current = state.Current;
        var next = new BarFrame(.7m,1,150,70);
        state.Set(next,"fight",true);
        check(state.Current == current,"rapid bar updates retarget without jumps");
        state.Advance(.2); check(state.Current == next,"bar settles exactly after final update");
        check(state.Pulse == 0,"pulse completes without being restarted by rapid hits");
        state.Advance(.3);
        state.Set(next with { Base = .35m, End = .5m, Share = 50 },"fight",true);
        check(state.Pulse == 0,"scale and percentage changes do not pulse");
        state.Advance(.2); state.Set(first,"new-fight",true);
        check(state.Current == first && !state.Active,"context switches snap without pulse");
        state.Set(next,"new-fight",false);
        check(state.Current == next && !state.Active,"hidden rows settle immediately");
        foreach(var interval in new[]{1d/30,1d/60,1d/144})
        {
            state.Snap(zero,"fps"); state.Set(first,"fps",true);
            var valid = true;
            for(int i=0;i<100;i++)
            {
                state.Advance(interval);
                valid &= state.Current.Base >= 0 && state.Current.Base <= state.Current.End && state.Current.End <= 1;
            }
            check(valid,"animated segment boundaries remain ordered");
            check(state.Current == first && !state.Active,"bar animation settles at different frame rates");
        }
        state.Set(zero,"fps",true);state.Advance(.2);
        check(state.Current == zero && state.Pulse == 0,"decreases and zero totals converge without pulse");
    }
}
