namespace Based.Spires2DamageMeter;
public sealed record AdditiveHistoryEntry(bool ThisTurn, bool Tagged, bool OwnerMatches);
public sealed record AdditiveSnapshot(string Model, decimal Amount, bool Powered, bool HasDealer, bool DealerOwner, bool TargetOwner, bool SameSide, bool HasCard, bool Tagged, bool IsOsty, bool PetOwnerMatches, bool RestrictedCommand, bool SourceMatches, bool AttackerMatches, int PriorFinished, IReadOnlyList<Share> Providers)
{
    public bool Eligible => Reload.RuntimeCalls.Call<bool>("AdditiveSnapshot.Eligible", this);
    public decimal Expected => Reload.RuntimeCalls.Call<decimal>("AdditiveSnapshot.Expected", this);

    public static int CountPrior(IEnumerable<AdditiveHistoryEntry> entries) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<int>("AdditiveSnapshot.CountPrior", entries);
    public static readonly string[] Models =
    {
        "StrengthPower",
        "AccuracyPower",
        "VigorPower",
        "CalcifyPower",
        "LeadershipPower",
        "PhantomBladesPower",
        "TaintedPower"
    };
}
