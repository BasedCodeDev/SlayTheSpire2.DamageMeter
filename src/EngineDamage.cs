namespace Based.Spires2DamageMeter;
public sealed record EngineDamage(decimal Hp, decimal Block, decimal Overkill)
{
    public const int AccountingVersion = 2;
    // STS2 LoseHpInternal already caps UnblockedDamage to HP-before minus HP-after.
    public static EngineDamage Normalize(decimal unblockedDamage, decimal blockedDamage, decimal overkillDamage) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<EngineDamage>("EngineDamage.Normalize", unblockedDamage, blockedDamage, overkillDamage);
}

public sealed class TraceCoverage
{
    public Dictionary<string, int> Outgoing { get; } = new();
    public Dictionary<string, int> Incoming { get; } = new();
    public int Matched => Outgoing.GetValueOrDefault("matched");
    public int Unmeasured => Outgoing.Where(p => p.Key != "matched").Sum(p => p.Value);

    public void Record(bool enemyReceiver, string outcome) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("TraceCoverage.Record", this, enemyReceiver, outcome);
    public void Clear() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("TraceCoverage.Clear", this);
    public static string IncomingOutcome(EngineDamage damage, bool redirected) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("TraceCoverage.IncomingOutcome", damage, redirected);
}
