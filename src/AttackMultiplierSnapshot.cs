namespace Based.Spires2DamageMeter;
public sealed record StartedAttack(bool ThisTurn, bool Attack, bool OwnerMatches);
public sealed record AttackMultiplierSnapshot(string Model, decimal Amount, bool Powered, bool HasCard, bool DealerOwner, bool DealerPet, bool CardOwner, bool TargetOwner, bool Restricted, bool SourceMatches, bool InPlay, int PlayIndex, int StartedCount, bool TargetWeak, bool DealerApplier, decimal DynamicValue, IReadOnlyList<Share> Providers)
{
    public static readonly string[] Models =
    {
        "DoubleDamagePower",
        "LethalityPower",
        "GigantificationPower",
        "ShrinkPower",
        "SlowPower",
        "TrackingPower",
        "KnockdownPower"
    };
    public static int CountStarted(IEnumerable<StartedAttack> entries) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<int>("AttackMultiplierSnapshot.CountStarted", entries);
    public decimal Expected => Reload.RuntimeCalls.Call<decimal>("AttackMultiplierSnapshot.Expected", this);
}
