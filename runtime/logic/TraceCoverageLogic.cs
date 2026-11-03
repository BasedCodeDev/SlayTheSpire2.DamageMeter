using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.TraceCoverage;

namespace Based.Spires2DamageMeter.Runtime;
internal static class TraceCoverageLogic
{
    public static void Record(TraceCoverage self, bool enemyReceiver, string outcome)
    {
        var counts = enemyReceiver ? self.Outgoing : self.Incoming;
        counts[outcome] = counts.GetValueOrDefault(outcome) + 1;
    }

    public static void Clear(TraceCoverage self)
    {
        self.Outgoing.Clear();
        self.Incoming.Clear();
    }

    public static string IncomingOutcome(EngineDamage damage, bool redirected) => redirected ? "redirected" : damage.Hp == 0 && damage.Block == 0 ? "prevention_or_redirection_unresolved" : damage.Hp == 0 ? "fully_blocked" : "resolved_hp_loss";
}
