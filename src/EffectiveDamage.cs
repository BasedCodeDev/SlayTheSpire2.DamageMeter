namespace Based.Spires2DamageMeter;
public sealed record EffectiveSnapshot(int Version, List<DamageCredit> Credits, Dictionary<string, List<DamageCredit>> Pending, bool Incomplete);
public sealed class EffectiveDamageLedger
{
    public const int Version = 1;
    public List<DamageCredit> Credits { get; } = new();
    public Dictionary<string, List<DamageCredit>> Pending { get; } = new();

    internal readonly HashSet<string> seen = new();
    public bool Incomplete { get; set; }

    public void Clear() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveDamageLedger.Clear", this);
    public void Discard(string target) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveDamageLedger.Discard", this, target);
    public void Finish() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveDamageLedger.Finish", this);
    public void Add(string id, string? target, IReadOnlyList<DamageCredit> parts, bool? breakthrough) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveDamageLedger.Add", this, id, target, parts, breakthrough);
    public EffectiveSnapshot Snapshot() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<EffectiveSnapshot>("EffectiveDamageLedger.Snapshot", this);
    public void Merge(EffectiveSnapshot s) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveDamageLedger.Merge", this, s);
    public CreditedDamage Ledger() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<CreditedDamage>("EffectiveDamageLedger.Ledger", this);
    public decimal PendingFor(string player) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<decimal>("EffectiveDamageLedger.PendingFor", this, player);
}
