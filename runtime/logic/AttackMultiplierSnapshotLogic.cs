using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.AttackMultiplierSnapshot;

namespace Based.Spires2DamageMeter.Runtime;
internal static class AttackMultiplierSnapshotLogic
{
    public static decimal Expected(AttackMultiplierSnapshot s) => !s.Powered ? 1 : s.Model switch
    {
        "DoubleDamagePower" => s.HasCard && (s.DealerOwner || s.DealerPet) ? 2 : 1,
        "LethalityPower" => s.HasCard && s.CardOwner && (!s.InPlay || s.PlayIndex <= 0) && s.StartedCount <= (s.InPlay ? 1 : 0) ? 1 + s.Amount / 100 : 1,
        "GigantificationPower" => s.HasCard && s.CardOwner && (!s.Restricted || s.SourceMatches) ? 3 : 1,
        "ShrinkPower" => s.DealerOwner ? (100 - s.DynamicValue) / 100 : 1,
        "SlowPower" => s.TargetOwner ? 1 + .1m * s.DynamicValue : 1,
        "TrackingPower" => s.HasCard && (s.DealerOwner || s.DealerPet) && s.TargetWeak ? s.Amount : 1,
        "KnockdownPower" => s.TargetOwner && !s.DealerApplier ? s.Amount : 1,
        _ => throw new InvalidOperationException("Unknown multiplier model")
    };
    public static int CountStarted(IEnumerable<StartedAttack> entries) => entries.Count(e => e.ThisTurn && e.Attack && e.OwnerMatches);
}
