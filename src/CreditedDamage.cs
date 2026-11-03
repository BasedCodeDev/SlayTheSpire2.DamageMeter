namespace Based.Spires2DamageMeter;
public sealed record DamageCredit(string EventId, string Player, string Kind, SourceIdentity Source, SourceIdentity? Trigger, decimal Hp, decimal Block, bool Amplification);
public sealed record AttributionSnapshot(int Version, List<DamageCredit> Credits, int Provisional);
public sealed class CreditedDamage
{
    public const int Version = 1;
    internal readonly HashSet<string> seen = new();
    public List<DamageCredit> Credits { get; } = new();
    public int Provisional { get; internal set; }

    public void Clear() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("CreditedDamage.Clear", this);
    public AttributionSnapshot Snapshot() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<AttributionSnapshot>("CreditedDamage.Snapshot", this);
    public void Merge(AttributionSnapshot state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("CreditedDamage.Merge", this, state);
    public IReadOnlyList<DamageCredit> Add(string id, string kind, decimal hp, decimal block, IReadOnlyList<Share> owners, SourceIdentity? trigger, AmplificationResult? amplification, bool confirmed = true) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<IReadOnlyList<DamageCredit>>("CreditedDamage.Add", this, id, kind, hp, block, owners, trigger, amplification, confirmed);
    public Dictionary<string, Totals> Rows(IEnumerable<SupportTransfer>? transfers = null) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Dictionary<string, Totals>>("CreditedDamage.Rows", this, transfers);
    public List<SourceTotals> Sources() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<List<SourceTotals>>("CreditedDamage.Sources", this);
}
