using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.AdditiveSnapshot;

namespace Based.Spires2DamageMeter.Runtime;
internal static class AdditiveSnapshotLogic
{
    public static bool Eligible(AdditiveSnapshot s) => s.Powered && (s.Model switch
    {
        "StrengthPower" => s.DealerOwner,
        "AccuracyPower" => s.DealerOwner && s.HasCard && s.Tagged,
        "VigorPower" => s.DealerOwner && (!s.RestrictedCommand || (s.HasCard && s.SourceMatches && s.AttackerMatches)),
        "CalcifyPower" => s.IsOsty && s.PetOwnerMatches,
        "LeadershipPower" => s.HasDealer && !s.DealerOwner && s.SameSide,
        "PhantomBladesPower" => s.DealerOwner && s.HasCard && s.Tagged && s.PriorFinished == 0,
        "TaintedPower" => s.TargetOwner,
        _ => throw new InvalidOperationException("Unknown additive model")
    });
    public static decimal Expected(AdditiveSnapshot s) => Eligible(s) ? s.Amount : 0;
    public static int CountPrior(IEnumerable<AdditiveHistoryEntry> entries) => entries.Count(e => e.ThisTurn && e.Tagged && e.OwnerMatches);
}
